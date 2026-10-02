const {test}=require('node:test');
const assert=require('node:assert/strict');
const {randomUUID,randomBytes}=require('node:crypto');
const {spawn}=require('node:child_process');
const {createServer}=require('node:net');
const {resolve}=require('node:path');
const fs=require('node:fs/promises');
const {Pool}=require('pg');
const {chromium}=require('playwright');

test('S106 full story: owner UI ↔ PostgreSQL ↔ independent anonymous customer', {timeout:120000}, async t=>{
  const url=process.env.TEST_DATABASE_URL;
  if(!url || new URL(url).pathname!=='/salon_test') throw new Error('TEST_DATABASE_URL must use the dedicated /salon_test database. Run database tests serially.');
  process.env.DATABASE_URL=url;process.env.JWT_SECRET=randomBytes(32).toString('hex');
  process.env.OWNER_EMAIL=`story-${randomUUID()}@salon.local`;process.env.OWNER_PASSWORD=randomBytes(20).toString('hex');
  const {createApp}=require('../dist/main');const app=await createApp();
  const pool=new Pool({connectionString:url});
  let browser,vite;const results=[];const pageErrors=[];let viteLogs='';
  t.after(async()=>{
    await browser?.close();
    if(vite && vite.exitCode===null) { const exited=new Promise(r=>vite.once('exit',r));vite.kill();await exited; }
    try { await pool.query('DELETE FROM owners WHERE email=$1',[process.env.OWNER_EMAIL]); }
    finally { await app.close();await pool.end(); }
  });
  await app.listen(0,'127.0.0.1');await pool.query('TRUNCATE stylist_services,stylists,services,service_groups');
  const apiBase=await app.getUrl();
  const port=await new Promise((res,rej)=>{const server=createServer();server.on('error',rej);server.listen(0,'127.0.0.1',()=>{const p=server.address().port;server.close(()=>res(p));});});
  const frontend=resolve(__dirname,'../../frontend');
  vite=spawn(process.execPath,[resolve(frontend,'node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port',String(port),'--strictPort'],{cwd:frontend,env:{...process.env,API_PROXY_TARGET:apiBase},windowsHide:true,stdio:['ignore','pipe','pipe']});
  vite.stdout.on('data',chunk=>viteLogs+=chunk);vite.stderr.on('data',chunk=>viteLogs+=chunk);
  const origin=`http://127.0.0.1:${port}`;let ready=false;
  for(let i=0;i<100;i++){try{if((await fetch(origin)).ok){ready=true;break;}}catch{}await new Promise(r=>setTimeout(r,100));}
  assert.ok(ready,`Vite did not start: ${viteLogs}`);
  browser=await chromium.launch({headless:true,...(process.env.PLAYWRIGHT_CHANNEL?{channel:process.env.PLAYWRIGHT_CHANNEL}:{})});
  const ownerContext=await browser.newContext({viewport:{width:1440,height:1000}});
  const guestContext=await browser.newContext({viewport:{width:1440,height:1000}});
  const owner=await ownerContext.newPage(),guest=await guestContext.newPage();
  owner.on('pageerror',e=>pageErrors.push(e.message));guest.on('pageerror',e=>pageErrors.push(e.message));
  const passed=message=>{results.push(message);console.log(`PASS: ${message}`);};
  await owner.goto(origin);await guest.goto(`${origin}/dat-lich`);
  await guest.getByRole('heading',{name:'Tiệm đang chuẩn bị những dịch vụ mới'}).waitFor();
  await owner.getByLabel('Email',{exact:true}).fill(process.env.OWNER_EMAIL);await owner.getByLabel('Mật khẩu',{exact:true}).fill(process.env.OWNER_PASSWORD);
  await owner.getByRole('button',{name:'Đăng nhập',exact:true}).click();await owner.getByRole('heading',{name:'Nhóm dịch vụ',exact:true}).waitFor();
  const getGroups=async()=> (await pool.query('SELECT id,name,display_order FROM service_groups ORDER BY display_order,created_at,id')).rows;
  const create=async(name,order)=>{
    await owner.getByRole('button',{name:'Thêm nhóm dịch vụ',exact:true}).first().click();await owner.getByLabel('Tên nhóm').fill(name);await owner.getByLabel('Thứ tự hiển thị').fill(String(order));
    await owner.getByRole('button',{name:'Tạo nhóm',exact:true}).click();await owner.getByRole('dialog').waitFor({state:'detached'});await owner.getByRole('button',{name:`Sửa ${name}`,exact:true}).waitFor();
    return (await getGroups()).find(g=>g.name===name).id;
  };
  const hair=await create('Tóc',1),wash=await create('Gội dưỡng',2),skin=await create('Chăm sóc da',3),empty=await create('Nhóm trống',4);
  const batch=async(group,prefix,active,inactive=0)=>{
    const data=Array.from({length:active+inactive},(_,i)=>({id:randomUUID(),group_id:group,name:`${prefix} ${String(i+1).padStart(4,'0')}`,is_active:i<active}));
    await pool.query('INSERT INTO services(id,group_id,name,is_active) SELECT id,group_id,name,is_active FROM jsonb_to_recordset($1::jsonb) AS s(id uuid,group_id uuid,name varchar(150),is_active boolean)',[JSON.stringify(data)]);
    return data;
  };
  // Service management UI is outside this story: fixtures assign services using real PostgreSQL.
  const hairServices=await batch(hair,'Tóc',5);await batch(wash,'Gội',5);const skinServices=await batch(skin,'Da',5);
  const fullStylist=randomUUID(),partialStylist=randomUUID();
  await pool.query('INSERT INTO stylists(id,name,is_active) VALUES($1,$2,true),($3,$4,true)',[fullStylist,'Linh Nguyễn',partialStylist,'Mai Trần']);
  await pool.query('INSERT INTO stylist_services(stylist_id,service_id) SELECT $1,id FROM services WHERE is_active=true AND id<>$2',[fullStylist,skinServices[4].id]);
  await pool.query('INSERT INTO stylist_services(stylist_id,service_id) VALUES($1,$2)',[partialStylist,hairServices[0].id]);
  await guest.waitForFunction(()=>document.querySelectorAll('.public-services li').length===15,{}, {timeout:5000});
  await owner.waitForFunction(()=>[...document.querySelectorAll('tbody tr')].filter(row=>row.textContent.includes('5 đang bán')).length===3,{}, {timeout:5000});
  assert.equal(await guest.getByRole('heading',{name:'Nhóm trống',exact:true}).count(),0);
  await guest.getByRole('button',{name:/Tóc 0001 Chọn dịch vụ/}).click();
  await guest.getByRole('button',{name:/Linh Nguyễn/}).waitFor();
  assert.equal(await guest.getByRole('button',{name:/Mai Trần/}).count(),1);
  await guest.getByRole('button',{name:/Tóc 0002 Chọn dịch vụ/}).click();
  await guest.getByRole('button',{name:/Mai Trần/}).waitFor({state:'detached'});
  await guest.getByRole('button',{name:/Linh Nguyễn/}).waitFor();
  await guest.getByRole('button',{name:/Linh Nguyễn/}).click();
  assert.equal(await guest.getByRole('button',{name:/Linh Nguyễn/}).getAttribute('aria-pressed'),'true');
  await guest.getByRole('button',{name:/Thợ bất kỳ/}).click();
  assert.equal(await guest.getByRole('button',{name:/Thợ bất kỳ/}).getAttribute('aria-pressed'),'true');
  await guest.getByRole('button',{name:/Da 0005 Chọn dịch vụ/}).click();
  await guest.getByText('Chưa có thợ nào có thể thực hiện toàn bộ dịch vụ đã chọn.').waitFor();
  await guest.getByRole('button',{name:/Da 0005 Đã chọn/}).click();
  await guest.getByRole('button',{name:/Tóc 0002 Đã chọn/}).click();
  await guest.getByRole('button',{name:/Tóc 0001 Đã chọn/}).click();
  passed('Booking: one service includes partial-capability stylist; multiple services exclude them; specific/any selection and no-match empty state work.');
  const consistency=async()=>{
    const login=await owner.evaluate(()=>JSON.parse(sessionStorage.getItem('salon-session')));
    const management=await (await fetch(`${apiBase}/api/service-groups`,{headers:{Authorization:`Bearer ${login.accessToken}`}})).json();
    const publicData=await (await fetch(`${apiBase}/api/public/service-groups`)).json();
    const db=(await pool.query('SELECT g.id,g.name,g.display_order,count(s.id)::int AS total,count(s.id) FILTER (WHERE s.is_active)::int AS active FROM service_groups g LEFT JOIN services s ON s.group_id=g.id GROUP BY g.id ORDER BY g.display_order,g.created_at,g.id')).rows;
    assert.deepEqual(management.map(g=>[g.id,g.name,g.displayOrder,g.serviceCount,g.activeServiceCount]),db.map(g=>[g.id,g.name,g.display_order,g.total,g.active]));
    assert.deepEqual(publicData.map(g=>[g.id,g.name,g.displayOrder,g.services.length]),db.filter(g=>g.active>0).map(g=>[g.id,g.name,g.display_order,g.active]));
    for(const group of publicData){const services=(await pool.query('SELECT id,name FROM services WHERE group_id=$1 AND is_active ORDER BY name,id',[group.id])).rows;assert.deepEqual(group.services,services);}
  };
  await consistency();passed('Create 4 groups through owner UI; assign 15 services in PostgreSQL; 3 equal-count groups remain distinct; both screens update live.');
  const edit=async(name,newName,order)=>{
    await owner.getByRole('button',{name:`Sửa ${name}`,exact:true}).click();await owner.getByLabel('Tên nhóm').fill(newName);await owner.getByLabel('Thứ tự hiển thị').fill(String(order));
    await owner.getByRole('button',{name:'Lưu thay đổi',exact:true}).click();await owner.getByRole('dialog').waitFor({state:'detached'});await owner.getByRole('button',{name:`Sửa ${newName}`,exact:true}).waitFor();
  };
  const waitOrder=async(names)=>guest.waitForFunction(expected=>JSON.stringify([...document.querySelectorAll('.public-group h3')].map(el=>el.textContent))===JSON.stringify(expected),names,{timeout:5000});
  await edit('Chăm sóc da','Chăm sóc da',0);await edit('Tóc','Tóc',2);await edit('Gội dưỡng','Gội dưỡng',3);
  await waitOrder(['Chăm sóc da','Tóc','Gội dưỡng']);await consistency();
  await edit('Gội dưỡng','Gội dưỡng',0);await edit('Chăm sóc da','Chăm sóc da',2);await edit('Tóc','Tóc',1);
  await waitOrder(['Gội dưỡng','Tóc','Chăm sóc da']);await consistency();
  passed('Two consecutive multi-group reorders reach the already-open anonymous customer page without reload.');
  await edit('Tóc','Tóc & tạo kiểu',1);await guest.getByRole('heading',{name:'Tóc & tạo kiểu',exact:true}).waitFor();
  assert.equal(await guest.getByRole('heading',{name:'Tóc',exact:true}).count(),0);
  assert.deepEqual((await pool.query('SELECT id FROM services WHERE group_id=$1 ORDER BY id',[hair])).rows.map(s=>s.id),hairServices.map(s=>s.id).sort());
  await consistency();passed('Rename a populated group: IDs and service assignments remain intact, public title updates immediately.');
  const snapshot=async()=>({groups:(await pool.query('SELECT * FROM service_groups ORDER BY id')).rows,services:(await pool.query('SELECT * FROM services ORDER BY id')).rows});
  const before=await snapshot();await owner.getByRole('button',{name:'Xoá Tóc & tạo kiểu',exact:true}).click();
  await owner.getByRole('alert').filter({hasText:'còn 5 dịch vụ đang bán'}).waitFor();assert.ok(await owner.getByRole('button',{name:'Xác nhận xoá'}).isDisabled());
  const session=await owner.evaluate(()=>JSON.parse(sessionStorage.getItem('salon-session')));
  const blocked=await fetch(`${apiBase}/api/service-groups/${hair}`,{method:'DELETE',headers:{Authorization:`Bearer ${session.accessToken}`}});assert.equal(blocked.status,409);
  assert.equal((await blocked.json()).activeServiceCount,5);assert.deepEqual(await snapshot(),before);
  await owner.getByRole('button',{name:'Huỷ',exact:true}).click();passed('Block deletion of a populated group through UI and API; full database snapshot is unchanged.');
  const big=await create('Nhóm số lượng lớn',10);await batch(big,'Dịch vụ lớn',1000,250);
  await guest.waitForFunction(()=>document.querySelectorAll('.public-services li').length===1015,{}, {timeout:10000});
  await owner.getByRole('button',{name:'Xoá Nhóm số lượng lớn',exact:true}).click();
  await owner.getByRole('alert').filter({hasText:'còn 1000 dịch vụ đang bán'}).waitFor();assert.match(await owner.getByRole('dialog').innerText(),/1250 dịch vụ/);
  const largeBefore=await snapshot();
  const largeDelete=await fetch(`${apiBase}/api/service-groups/${big}`,{method:'DELETE',headers:{Authorization:`Bearer ${session.accessToken}`}});assert.equal(largeDelete.status,409);
  const largeResult=await largeDelete.json();assert.equal(largeResult.serviceCount,1250);assert.equal(largeResult.activeServiceCount,1000);assert.deepEqual(await snapshot(),largeBefore);
  await consistency();
  if(process.env.STORY_REPORT_DIR){await fs.mkdir(process.env.STORY_REPORT_DIR,{recursive:true});await owner.screenshot({path:resolve(process.env.STORY_REPORT_DIR,'lat4-1250-dich-vu.png'),fullPage:true});}
  await owner.getByRole('button',{name:'Huỷ',exact:true}).click();passed('Large group: 1,250 total / 1,000 active; exact UI/API counts, 1,000 public services, failed delete changes no data.');
  await owner.getByRole('button',{name:'Xoá Nhóm trống',exact:true}).click();
  await owner.waitForFunction(()=>[...document.querySelectorAll('dialog button')].some(b=>b.textContent==='Xác nhận xoá'&&!b.disabled));
  await owner.getByRole('button',{name:'Xác nhận xoá'}).click();await owner.getByRole('dialog').waitFor({state:'detached'});
  assert.equal((await pool.query('SELECT id FROM service_groups WHERE id=$1',[empty])).rowCount,0);
  assert.equal(await guest.getByRole('heading',{name:'Nhóm trống',exact:true}).count(),0);await consistency();
  passed('Delete an empty group; it is absent in owner data, PostgreSQL and public catalog (empty groups were already hidden).');
  await pool.query('UPDATE services SET group_id=NULL WHERE group_id=$1',[wash]);
  await guest.getByRole('heading',{name:'Gội dưỡng',exact:true}).waitFor({state:'detached'});
  await owner.getByRole('button',{name:'Xoá Gội dưỡng',exact:true}).click();
  await owner.waitForFunction(()=>[...document.querySelectorAll('dialog button')].some(b=>b.textContent==='Xác nhận xoá'&&!b.disabled));
  await owner.getByRole('button',{name:'Xác nhận xoá'}).click();await owner.getByRole('dialog').waitFor({state:'detached'});
  await consistency();passed('A previously visible group disappears live when emptied, then deletes successfully.');
  await guest.reload();await guest.getByRole('heading',{name:'Tóc & tạo kiểu',exact:true}).waitFor();await consistency();
  await guest.setViewportSize({width:390,height:844});assert.ok(await guest.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  assert.deepEqual(pageErrors,[]);assert.deepEqual(await guest.evaluate(()=>Object.keys(sessionStorage)),[]);
  passed('Reload retains final public data; mobile 390px has no horizontal overflow; no browser exceptions; customer remains unauthenticated.');
  if(process.env.STORY_REPORT_DIR) await fs.writeFile(resolve(process.env.STORY_REPORT_DIR,'lat4-ket-qua.json'),JSON.stringify({passed:true,checkedAt:new Date().toISOString(),results,largeGroup:{total:1250,active:1000},serviceAssignment:'PostgreSQL fixtures; service-management UI is outside scope'},null,2));
});
