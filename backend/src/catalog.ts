import { Controller, Get, Header, Injectable, Logger, OnModuleDestroy, OnModuleInit, Sse } from '@nestjs/common';
import { Client } from 'pg';
import { interval, map, merge, startWith, Subject } from 'rxjs';
import { Database } from './database';

@Injectable()
export class CatalogService implements OnModuleInit, OnModuleDestroy {
  private readonly changed = new Subject<void>();
  private readonly logger = new Logger(CatalogService.name);
  private listener?: Client;
  private retry?: NodeJS.Timeout;
  private stopped = false;
  constructor(private readonly db: Database) {}

  async list() {
    // One statement provides a consistent snapshot; the inner join hides empty groups.
    const { rows } = await this.db.pool.query(`
      SELECT g.id, g.name, g.display_order AS "displayOrder",
        json_agg(json_build_object('id',s.id,'name',s.name) ORDER BY s.name,s.id) AS services
      FROM service_groups g JOIN services s ON s.group_id=g.id AND s.is_active=true
      GROUP BY g.id ORDER BY g.display_order,g.created_at,g.id`);
    return rows;
  }

  events() {
    return merge(
      this.changed.pipe(map(() => ({ type: 'catalog-changed', data: 'refresh' }))),
      interval(25000).pipe(map(() => ({ type: 'heartbeat', data: 'keep-alive' }))),
    ).pipe(startWith({ type: 'catalog-changed', data: 'refresh' }));
  }

  async onModuleInit() { await this.connect(); }
  private async connect() {
    if (this.stopped) return;
    const client = new Client({ connectionString: process.env.DATABASE_URL });
    this.listener = client;
    let retryScheduled = false;
    const reconnect = () => {
      if (this.stopped || retryScheduled) return;
      retryScheduled = true;
      this.logger.warn('Catalog notifications disconnected; reconnecting.');
      void client.end().catch(() => undefined);
      this.retry = setTimeout(() => { void this.connect(); }, 2000);
    };
    client.on('notification', () => this.changed.next());
    client.on('error', reconnect);
    client.on('end', reconnect);
    try {
      await client.connect();
      if (this.stopped) { await client.end(); return; }
      await client.query('LISTEN salon_catalog_changed');
      // Refresh subscribers after reconnection to catch changes made while disconnected.
      this.changed.next();
    } catch { reconnect(); }
  }
  async onModuleDestroy() {
    this.stopped = true;
    clearTimeout(this.retry);
    this.changed.complete();
    await this.listener?.end().catch(() => undefined);
  }
}

@Controller('public')
export class CatalogController {
  constructor(private readonly catalog: CatalogService) {}
  @Get('service-groups')
  @Header('Cache-Control', 'no-store')
  list() { return this.catalog.list(); }

  @Sse('catalog-events')
  @Header('X-Accel-Buffering', 'no')
  events() { return this.catalog.events(); }
}
