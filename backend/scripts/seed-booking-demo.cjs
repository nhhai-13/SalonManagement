// Explicit opt-in sample data. Does not delete or replace existing groups/services.
require('dotenv/config');
const {Pool}=require('pg');
const {randomUUID}=require('node:crypto');
async function main(){
  if(!process.env.DATABASE_URL) throw new Error('Thiếu DATABASE_URL. Chạy script từ thư mục backend.');
  const pool=new Pool({connectionString:process.env.DATABASE_URL});
  const client=await pool.connect();
  try{
    await client.query('BEGIN');
    const data=[['Chăm sóc da',1,['Chăm sóc da cơ bản','Làm sạch sâu','Dưỡng ẩm phục hồi']],['Tóc',2,['Cắt & tạo kiểu tóc','Uốn tóc','Chăm sóc tóc chuyên sâu']],['Gội dưỡng',3,['Gội dưỡng thảo mộc','Gội đầu thư giãn']]];
    for(const [name,order,names] of data){
      const result=await client.query('INSERT INTO service_groups(id,name,name_key,display_order) VALUES($1,$2,$3,$4) ON CONFLICT(name_key) DO UPDATE SET display_order=EXCLUDED.display_order,updated_at=now() RETURNING id',[randomUUID(),name,name.toLocaleLowerCase('vi'),order]);
      const id=result.rows[0].id;
      for(const serviceName of names) await client.query('INSERT INTO services(id,group_id,name,is_active) SELECT $1::uuid,$2::uuid,$3::varchar,true WHERE NOT EXISTS(SELECT 1 FROM services WHERE group_id=$2::uuid AND name=$3::varchar)',[randomUUID(),id,serviceName]);
    }
    await client.query('COMMIT');console.log('Đã chuẩn bị 3 nhóm / 8 dịch vụ mẫu. Không xoá dữ liệu hiện có.');
  }catch(error){await client.query('ROLLBACK');throw error;}
  finally{client.release();await pool.end();}
}
main().catch(error=>{console.error(error);process.exitCode=1;});
