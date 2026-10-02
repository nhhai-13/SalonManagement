// Explicit opt-in demo seed. Adds a full-capability stylist and a partial-capability stylist.
require('dotenv/config');
const { Pool } = require('pg');
const { randomUUID } = require('node:crypto');

async function main() {
  if (!process.env.DATABASE_URL) throw new Error('Thiếu DATABASE_URL. Chạy script từ thư mục backend.');
  const pool = new Pool({ connectionString: process.env.DATABASE_URL });
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    const services = await client.query('SELECT id,name FROM services WHERE is_active=true ORDER BY name,id');
    if (!services.rowCount) throw new Error('Chưa có dịch vụ đang bán. Hãy chạy seed-booking-demo.cjs trước.');
    const full = await client.query(`INSERT INTO stylists(id,name,is_active) VALUES($1,'Linh Nguyễn',true)
      ON CONFLICT(name) DO UPDATE SET is_active=true RETURNING id`, [randomUUID()]);
    const partial = await client.query(`INSERT INTO stylists(id,name,is_active) VALUES($1,'Mai Trần',true)
      ON CONFLICT(name) DO UPDATE SET is_active=true RETURNING id`, [randomUUID()]);
    await client.query('DELETE FROM stylist_services WHERE stylist_id=ANY($1::uuid[])', [[full.rows[0].id, partial.rows[0].id]]);
    for (const service of services.rows) {
      await client.query('INSERT INTO stylist_services(stylist_id,service_id) VALUES($1,$2)', [full.rows[0].id, service.id]);
    }
    // Mai demonstrates the partial-match case: can perform only the first active service.
    await client.query('INSERT INTO stylist_services(stylist_id,service_id) VALUES($1,$2)', [partial.rows[0].id, services.rows[0].id]);
    await client.query('COMMIT');
    console.log(`Đã chuẩn bị 2 thợ mẫu cho ${services.rowCount} dịch vụ đang bán.`);
  } catch (error) { await client.query('ROLLBACK'); throw error; }
  finally { client.release(); await pool.end(); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
