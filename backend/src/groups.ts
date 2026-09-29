import { Body, ConflictException, Controller, Delete, Get, Header, Injectable, NotFoundException, Param, ParseUUIDPipe, Post, Put, UseGuards } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import { Database } from './database';
import { GroupDto } from './dto';
import { OwnerGuard } from './auth';

@Injectable()
export class GroupsService {
  constructor(private readonly db: Database) {}
  async deletionCheck(id: string) {
    const { rows } = await this.db.pool.query(`SELECT g.id,g.name,
      count(s.id)::int AS "serviceCount",count(s.id) FILTER (WHERE s.is_active)::int AS "activeServiceCount"
      FROM service_groups g LEFT JOIN services s ON s.group_id=g.id WHERE g.id=$1 GROUP BY g.id`, [id]);
    if (!rows.length) throw new NotFoundException('Nhóm dịch vụ không còn tồn tại.');
    return { ...rows[0], canDelete: rows[0].activeServiceCount === 0 };
  }
  async list() {
    const { rows } = await this.db.pool.query(`SELECT g.id,g.name,g.display_order AS "displayOrder",g.updated_at AS "updatedAt",
      count(s.id)::int AS "serviceCount",count(s.id) FILTER (WHERE s.is_active)::int AS "activeServiceCount"
      FROM service_groups g LEFT JOIN services s ON s.group_id=g.id GROUP BY g.id
      ORDER BY g.display_order,g.created_at,g.id`);
    return rows;
  }
  async save(dto: GroupDto, id?: string) {
    try {
      const result = id
        ? await this.db.pool.query('UPDATE service_groups SET name=$1,display_order=$2,updated_at=now(),name_key=$4 WHERE id=$3 RETURNING id,name,display_order AS "displayOrder"', [dto.name, dto.displayOrder, id, dto.name.toLocaleLowerCase('vi')])
        : await this.db.pool.query('INSERT INTO service_groups(id,name,display_order,name_key) VALUES($1,$2,$3,$4) RETURNING id,name,display_order AS "displayOrder"', [randomUUID(), dto.name, dto.displayOrder, dto.name.toLocaleLowerCase('vi')]);
      if (!result.rowCount) throw new NotFoundException('Nhóm dịch vụ không còn tồn tại.');
      return result.rows[0];
    } catch (error) {
      if ((error as {code?: string}).code === '23505') throw new ConflictException('Tên nhóm đã tồn tại. Vui lòng chọn tên khác.');
      throw error;
    }
  }
  async remove(id: string) {
    const client = await this.db.pool.connect();
    try {
      await client.query('BEGIN');
      // Parent lock serializes group deletion with new service assignments (foreign key checks).
      const group = await client.query('SELECT id FROM service_groups WHERE id=$1 FOR UPDATE', [id]);
      if (!group.rowCount) throw new NotFoundException('Nhóm dịch vụ không còn tồn tại.');
      // Lock all children, including inactive ones, against concurrent status changes.
      const services = await client.query('SELECT is_active FROM services WHERE group_id=$1 FOR UPDATE', [id]);
      const activeCount = services.rows.filter(row => row.is_active).length;
      if (activeCount) throw new ConflictException({
        statusCode: 409, code: 'GROUP_HAS_ACTIVE_SERVICES',
        message: `Không thể xoá: nhóm có tổng cộng ${services.rows.length} dịch vụ, trong đó ${activeCount} dịch vụ đang bán.`,
        serviceCount: services.rows.length, activeServiceCount: activeCount, canDelete: false,
      });
      // Inactive services are retained and become ungrouped by ON DELETE SET NULL.
      await client.query('DELETE FROM service_groups WHERE id=$1', [id]);
      await client.query('COMMIT');
      return { message: 'Đã xoá nhóm dịch vụ.' };
    } catch (error) { await client.query('ROLLBACK'); throw error; }
    finally { client.release(); }
  }
}
@Controller('service-groups')
@UseGuards(OwnerGuard)
export class GroupsController {
  constructor(private readonly groups: GroupsService) {}
  @Get() list() { return this.groups.list(); }
  @Get(':id/deletion-check')
  @Header('Cache-Control', 'no-store')
  deletionCheck(@Param('id', ParseUUIDPipe) id: string) { return this.groups.deletionCheck(id); }
  @Post() create(@Body() dto: GroupDto) { return this.groups.save(dto); }
  @Put(':id') update(@Param('id', ParseUUIDPipe) id: string, @Body() dto: GroupDto) { return this.groups.save(dto, id); }
  @Delete(':id') remove(@Param('id', ParseUUIDPipe) id: string) { return this.groups.remove(id); }
}
