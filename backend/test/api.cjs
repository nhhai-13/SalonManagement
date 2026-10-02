const { test } = require('node:test');
const assert = require('node:assert/strict');
const { randomUUID, randomBytes } = require('node:crypto');
const { Pool } = require('pg');
const bcrypt = require('bcrypt');
const { JwtService } = require('@nestjs/jwt');

test('PostgreSQL API: owner authentication, validation, CRUD, service constraints and persistence', async t => {
  const url=process.env.TEST_DATABASE_URL;
  if (!url || new URL(url).pathname !== '/salon_test') throw new Error('Set TEST_DATABASE_URL to a dedicated /salon_test database.');
  process.env.DATABASE_URL=url;
  process.env.JWT_SECRET=randomBytes(32).toString('hex');
  process.env.OWNER_EMAIL='test-owner@salon.local';
  process.env.OWNER_PASSWORD='Test-Only-Password-2026';
  const {createApp}=require('../dist/main');
  const app=await createApp();
  const pool=new Pool({connectionString:url});
  t.after(async()=>{await app.close();await pool.end();});
  await app.listen(0,'127.0.0.1');
  await pool.query('TRUNCATE stylist_services,stylists,services,service_groups,owners');
  const ownerId=randomUUID();
  await pool.query('INSERT INTO owners(id,email,password_hash,role) VALUES($1,$2,$3,$4)',[ownerId,process.env.OWNER_EMAIL,await bcrypt.hash(process.env.OWNER_PASSWORD,12),'owner']);
  const base=await app.getUrl(); let token='';
  const call=async(path,method='GET',body,customToken=token)=>{
    const r=await fetch(`${base}/api${path}`,{method,headers:{'Content-Type':'application/json',...(customToken?{Authorization:`Bearer ${customToken}`}:{})},body:body===undefined?undefined:JSON.stringify(body)});
    return {status:r.status,data:await r.json()};
  };
  await t.test('Missing, forged and expired tokens are rejected',async()=>{
    assert.equal((await call('/service-groups')).status,401);
    assert.equal((await call('/service-groups','GET',undefined,'forged')).status,401);
    const expired=new JwtService().sign({sub:ownerId,role:'owner'},{secret:process.env.JWT_SECRET,expiresIn:-1,issuer:'salon-api',audience:'salon-owner'});
    assert.equal((await call('/service-groups','GET',undefined,expired)).status,401);
  });
  await t.test('Wrong password is rejected; owner can log in',async()=>{
    assert.equal((await call('/auth/login','POST',{email:process.env.OWNER_EMAIL,password:'wrong'})).status,401);
    const login=await call('/auth/login','POST',{email:process.env.OWNER_EMAIL,password:process.env.OWNER_PASSWORD});
    assert.equal(login.status,201);token=login.data.accessToken;assert.ok(token);
  });
  const ids=[];
  await t.test('Create Tóc, Gội dưỡng, Chăm sóc da and return sorted list',async()=>{
    for(const [name,displayOrder] of [['Tóc',3],['Gội dưỡng',1],['Chăm sóc da',2]]){
      const r=await call('/service-groups','POST',{name,displayOrder});assert.equal(r.status,201);ids.push(r.data.id);
    }
    const list=await call('/service-groups');assert.deepEqual(list.data.map(g=>g.name),['Gội dưỡng','Chăm sóc da','Tóc']);
  });
  await t.test('Blank and duplicate names, invalid orders and extra fields are rejected',async()=>{
    for(const body of [{name:'   ',displayOrder:1},{name:'A',displayOrder:-1},{name:'A',displayOrder:1.5},{name:'A',displayOrder:'2'},{name:'A',displayOrder:1,role:'owner'}]) assert.equal((await call('/service-groups','POST',body)).status,400);
    assert.equal((await call('/service-groups','POST',{name:'  tóc  ',displayOrder:5})).status,409);
    assert.equal((await call('/service-groups','POST',{name:'CHĂM SÓC DA'.normalize('NFD'),displayOrder:5})).status,409);
    assert.equal((await call(`/service-groups/${ids[1]}`,'PUT',{name:'Tóc',displayOrder:4})).status,409);
  });
  await t.test('Public catalog hides empty/inactive groups, groups services correctly and updates live',async()=>{
    const inactiveGroup=await call('/service-groups','POST',{name:'Chỉ ngừng bán',displayOrder:0});
    const emptyGroup=await call('/service-groups','POST',{name:'Nhóm trống',displayOrder:0});
    const fixtures=[
      [randomUUID(),ids[0],'Cắt tóc',true], [randomUUID(),ids[0],'Uốn tóc',true],
      [randomUUID(),ids[1],'Gội dưỡng thảo mộc',true], [randomUUID(),ids[2],'Chăm sóc da cơ bản',true],
      [randomUUID(),ids[2],'Dịch vụ ngừng bán',false], [randomUUID(),inactiveGroup.data.id,'Không công khai',false],
      [randomUUID(),null,'Dịch vụ chưa phân nhóm',true]
    ];
    for(const row of fixtures) await pool.query('INSERT INTO services(id,group_id,name,is_active) VALUES($1,$2,$3,$4)',row);
    const publicRead=()=>call('/public/service-groups','GET',undefined,'');
    let result=await publicRead();assert.equal(result.status,200);
    assert.deepEqual(result.data.map(g=>g.name),['Gội dưỡng','Chăm sóc da','Tóc']);
    assert.deepEqual(result.data.find(g=>g.id===ids[0]).services.map(s=>s.name),['Cắt tóc','Uốn tóc']);
    assert.deepEqual(result.data.find(g=>g.id===ids[2]).services.map(s=>s.name),['Chăm sóc da cơ bản']);
    assert.deepEqual(Object.keys(result.data[0]).sort(),['displayOrder','id','name','services']);
    assert.deepEqual(Object.keys(result.data[0].services[0]).sort(),['id','name']);
    const response=await fetch(`${base}/api/public/service-groups`);
    assert.equal(response.headers.get('cache-control'),'no-store');
    await response.json();
    const abort=new AbortController();
    const timeout=setTimeout(()=>abort.abort(),8000);
    try {
      const stream=await fetch(`${base}/api/public/catalog-events`,{signal:abort.signal});
      assert.equal(stream.status,200);
      const reader=stream.body.getReader();
      let buffer='';const decoder=new TextDecoder();
      const nextChange=async()=>{
        while(true){
          const end=buffer.indexOf('\n\n');
          if(end!==-1){const event=buffer.slice(0,end);buffer=buffer.slice(end+2);if(event.includes('event: catalog-changed')) return;continue;}
          const chunk=await reader.read();if(chunk.done) throw new Error('SSE ended early');buffer+=decoder.decode(chunk.value,{stream:true}).replace(/\r/g,'');
        }
      };
      await nextChange();
      for(const [id,name,displayOrder] of [[ids[2],'Chăm sóc da',1],[ids[0],'Tóc',2],[ids[1],'Gội dưỡng',3]]) {
        assert.equal((await call(`/service-groups/${id}`,'PUT',{name,displayOrder})).status,200);
      }
      await nextChange();
      result=await publicRead();assert.deepEqual(result.data.map(g=>g.name),['Chăm sóc da','Tóc','Gội dưỡng']);
      await pool.query('UPDATE services SET is_active=false WHERE group_id=$1',[ids[1]]);
      result=await publicRead();assert.deepEqual(result.data.map(g=>g.name),['Chăm sóc da','Tóc']);
      await pool.query('UPDATE services SET group_id=$1 WHERE id=$2',[ids[2],fixtures[0][0]]);
      result=await publicRead();assert.ok(result.data[0].services.some(s=>s.name==='Cắt tóc'));
      assert.equal(result.data.find(g=>g.id===ids[0]).services.some(s=>s.name==='Cắt tóc'),false);
      await pool.query('UPDATE service_groups SET display_order=1 WHERE id=ANY($1::uuid[])',[ids]);
      const adminOrder=(await call('/service-groups')).data.filter(g=>g.activeServiceCount>0).map(g=>g.id);
      assert.deepEqual((await publicRead()).data.map(g=>g.id),adminOrder);
    } finally { clearTimeout(timeout);abort.abort(); }
    await pool.query('DELETE FROM services WHERE id=ANY($1::uuid[])',[fixtures.map(row=>row[0])]);
    assert.deepEqual((await publicRead()).data,[]);
    await call(`/service-groups/${inactiveGroup.data.id}`,'DELETE');
    await call(`/service-groups/${emptyGroup.data.id}`,'DELETE');
  });
  await t.test('Public stylist lookup requires every selected active service',async()=>{
    const serviceOne=randomUUID(),serviceTwo=randomUUID(),inactiveService=randomUUID();
    await pool.query('INSERT INTO services(id,group_id,name,is_active) VALUES($1,NULL,$2,true),($3,NULL,$4,true),($5,NULL,$6,false)',[serviceOne,'Thử dịch vụ một',serviceTwo,'Thử dịch vụ hai',inactiveService,'Dịch vụ ngừng bán']);
    const allId=randomUUID(), partialId=randomUUID(), inactiveId=randomUUID();
    await pool.query('INSERT INTO stylists(id,name,is_active) VALUES($1,$2,true),($3,$4,true),($5,$6,false)',[allId,'Thợ đủ dịch vụ',partialId,'Thợ chỉ làm một phần',inactiveId,'Thợ ngừng hoạt động']);
    await pool.query('INSERT INTO stylist_services(stylist_id,service_id) VALUES($1,$3),($1,$4),($2,$3),($5,$3),($5,$4)',[allId,partialId,serviceOne,serviceTwo,inactiveId]);
    const lookup=async(ids)=>call(`/public/stylists?serviceIds=${ids.join(',')}`,'GET',undefined,'');
    let result=await lookup([serviceOne]);
    assert.deepEqual(result.data.map(stylist=>stylist.id).sort(),[allId,partialId].sort());
    result=await lookup([serviceOne,serviceTwo]);
    assert.deepEqual(result.data.map(stylist=>stylist.id),[allId]);
    assert.deepEqual((await lookup([inactiveService])).data,[]);
    const noMatch=await lookup([randomUUID()]);assert.deepEqual(noMatch.data,[]);
    assert.equal((await lookup(['invalid'])).status,400);
    assert.equal((await call('/public/stylists','GET',undefined,'')).status,400);
    assert.equal((await fetch(`${base}/api/public/stylists?serviceIds=${serviceOne}`)).headers.get('cache-control'),'no-store');
  });
  await t.test('Edit name and order persists in PostgreSQL',async()=>{
    assert.equal((await call(`/service-groups/${ids[0]}`,'PUT',{name:'Tóc cao cấp',displayOrder:0})).status,200);
    assert.equal((await call('/service-groups')).data[0].name,'Tóc cao cấp');
    const db=await pool.query('SELECT name,display_order FROM service_groups WHERE id=$1',[ids[0]]);
    assert.equal(db.rows[0].name,'Tóc cao cấp');assert.equal(db.rows[0].display_order,0);
  });
  await t.test('Active services block deletion with exact count and preserve data',async()=>{
    await pool.query('INSERT INTO services(id,group_id,name,is_active) VALUES($1,$2,$3,true),($4,$2,$5,true)',[randomUUID(),ids[0],'Cắt tóc',randomUUID(),'Uốn tóc']);
    const r=await call(`/service-groups/${ids[0]}`,'DELETE');assert.equal(r.status,409);assert.match(r.data.message,/2 dịch vụ/);
    assert.equal((await pool.query('SELECT * FROM service_groups WHERE id=$1',[ids[0]])).rowCount,1);
    assert.equal((await call('/service-groups')).data.find(g=>g.id===ids[0]).activeServiceCount,2);
  });
  await t.test('Delete empty group; retain inactive services after group deletion',async()=>{
    assert.equal((await call(`/service-groups/${ids[2]}`,'DELETE')).status,200);
    const sid=randomUUID();await pool.query('INSERT INTO services(id,group_id,name,is_active) VALUES($1,$2,$3,false)',[sid,ids[1],'Gội dưỡng cũ']);
    assert.equal((await call(`/service-groups/${ids[1]}`,'DELETE')).status,200);
    assert.equal((await pool.query('SELECT group_id FROM services WHERE id=$1',[sid])).rows[0].group_id,null);
    assert.equal((await call(`/service-groups/${ids[2]}`,'DELETE')).status,404);
    assert.equal((await call('/service-groups/not-a-uuid','DELETE')).status,400);
  });
  await t.test('Deletion checks 1, 5 and 0 active services, reports total separately and preserves all data',async()=>{
    const created=await call('/service-groups','POST',{name:'Tóc',displayOrder:8});
    const id=created.data.id, serviceIds=[];
    const add=async(active)=>{const sid=randomUUID();serviceIds.push(sid);await pool.query('INSERT INTO services(id,group_id,name,is_active) VALUES($1,$2,$3,$4)',[sid,id,`Dịch vụ ${serviceIds.length}`,active]);};
    const snapshot=async()=>({group:(await pool.query('SELECT * FROM service_groups WHERE id=$1',[id])).rows,services:(await pool.query('SELECT * FROM services WHERE group_id=$1 ORDER BY id',[id])).rows});
    await add(true);
    let before=await snapshot();
    let blocked=await call(`/service-groups/${id}`,'DELETE');
    assert.equal(blocked.status,409);assert.equal(blocked.data.activeServiceCount,1);assert.equal(blocked.data.serviceCount,1);
    assert.deepEqual(await snapshot(),before);
    for(let i=0;i<4;i++) await add(true);
    before=await snapshot();
    let check=await call(`/service-groups/${id}/deletion-check`);
    assert.equal(check.data.canDelete,false);assert.equal(check.data.activeServiceCount,5);
    blocked=await call(`/service-groups/${id}`,'DELETE');
    assert.equal(blocked.status,409);assert.equal(blocked.data.code,'GROUP_HAS_ACTIVE_SERVICES');
    assert.equal(blocked.data.activeServiceCount,5);assert.equal(blocked.data.serviceCount,5);
    assert.match(blocked.data.message,/5 dịch vụ đang bán/);assert.deepEqual(await snapshot(),before);
    await add(false);await add(false);
    blocked=await call(`/service-groups/${id}`,'DELETE');
    assert.equal(blocked.data.serviceCount,7);assert.equal(blocked.data.activeServiceCount,5);
    check=await call(`/service-groups/${id}/deletion-check`);assert.equal(check.data.serviceCount,7);
    assert.equal((await call(`/service-groups/${id}/deletion-check`,'GET',undefined,'')).status,401);
    await pool.query('UPDATE services SET is_active=false WHERE group_id=$1',[id]);
    check=await call(`/service-groups/${id}/deletion-check`);assert.equal(check.data.canDelete,true);assert.equal(check.data.activeServiceCount,0);
    const retained=(await pool.query('SELECT id,name,is_active FROM services WHERE group_id=$1 ORDER BY id',[id])).rows;
    assert.equal((await call(`/service-groups/${id}`,'DELETE')).status,200);
    assert.equal((await pool.query('SELECT id FROM service_groups WHERE id=$1',[id])).rowCount,0);
    const after=(await pool.query('SELECT id,name,is_active,group_id FROM services WHERE id=ANY($1::uuid[]) ORDER BY id',[serviceIds])).rows;
    assert.deepEqual(after.map(({group_id,...rest})=>rest),retained);assert.ok(after.every(s=>s.group_id===null));
    assert.equal((await call(`/service-groups/${id}/deletion-check`)).status,404);
  });
  await t.test('A service activated concurrently after precheck prevents deletion under transaction locks',async()=>{
    const created=await call('/service-groups','POST',{name:'Nhóm kiểm thử đồng thời',displayOrder:9});
    const id=created.data.id,sid=randomUUID();
    await pool.query('INSERT INTO services(id,group_id,name,is_active) VALUES($1,$2,$3,false)',[sid,id,'Dịch vụ đổi trạng thái']);
    assert.equal((await call(`/service-groups/${id}/deletion-check`)).data.canDelete,true);
    const client=await pool.connect();let pending;
    try{
      await client.query('BEGIN');
      await client.query('UPDATE services SET is_active=true WHERE id=$1',[sid]);
      pending=call(`/service-groups/${id}`,'DELETE');
      let waiting=false;
      for(let i=0;i<100;i++){
        const result=await pool.query("SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND query LIKE 'SELECT is_active FROM services WHERE group_id=%' AND wait_event_type='Lock'");
        if(result.rowCount){waiting=true;break;}
        await new Promise(resolve=>setTimeout(resolve,20));
      }
      assert.ok(waiting,'DELETE must wait for the service status transaction');
      await client.query('COMMIT');
      const blocked=await pending;assert.equal(blocked.status,409);assert.equal(blocked.data.activeServiceCount,1);
      assert.equal((await pool.query('SELECT group_id,is_active FROM services WHERE id=$1',[sid])).rows[0].group_id,id);
    }finally{await client.query('ROLLBACK');client.release();if(pending) await pending;}
    await pool.query('UPDATE services SET is_active=false WHERE id=$1',[sid]);
    assert.equal((await call(`/service-groups/${id}`,'DELETE')).status,200);
  });
  await t.test('Role changes revoke owner access immediately',async()=>{
    await pool.query("UPDATE owners SET role='staff' WHERE id=$1",[ownerId]);
    assert.equal((await call('/service-groups')).status,403);
    assert.equal((await call('/service-groups','POST',{name:'Forbidden',displayOrder:1})).status,403);
    assert.equal((await call(`/service-groups/${ids[0]}`,'PUT',{name:'Forbidden',displayOrder:1})).status,403);
    assert.equal((await call(`/service-groups/${ids[0]}`,'DELETE')).status,403);
  });
  await t.test('Repeated wrong logins are rate limited',async()=>{
    let result;
    for(let i=0;i<4;i++) result=await call('/auth/login','POST',{email:'unknown@salon.local',password:'incorrect'});
    assert.equal(result.status,429);
  });
});
