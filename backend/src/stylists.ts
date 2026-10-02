import { BadRequestException, Controller, Get, Header, Injectable, Query } from '@nestjs/common';
import { Database } from './database';

const UUID = /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i;

@Injectable()
export class StylistsService {
  constructor(private readonly db: Database) {}

  async listForServices(value?: string) {
    if (typeof value !== 'string' || !value) throw new BadRequestException('Vui lòng chọn ít nhất một dịch vụ.');
    const ids = [...new Set(value.split(',').map(id => id.trim()))];
    if (!ids.length || ids.some(id => !UUID.test(id))) throw new BadRequestException('Danh sách mã dịch vụ không hợp lệ.');

    const { rows } = await this.db.pool.query(`
      SELECT st.id, st.name
      FROM stylists st
      WHERE st.is_active = true
        AND (SELECT count(*)
             FROM services s
             WHERE s.id = ANY($1::uuid[]) AND s.is_active = true)
            = cardinality($1::uuid[])
        AND (SELECT count(DISTINCT ss.service_id)
             FROM stylist_services ss
             WHERE ss.stylist_id = st.id AND ss.service_id = ANY($1::uuid[]))
            = cardinality($1::uuid[])
      ORDER BY st.name, st.id`, [ids]);
    return rows;
  }
}

@Controller('public/stylists')
export class StylistsController {
  constructor(private readonly stylists: StylistsService) {}

  @Get()
  @Header('Cache-Control', 'no-store')
  list(@Query('serviceIds') serviceIds?: string) {
    return this.stylists.listForServices(serviceIds);
  }
}
