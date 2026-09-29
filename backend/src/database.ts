import { Injectable, OnModuleInit, OnModuleDestroy } from '@nestjs/common';
import { Pool } from 'pg';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { randomUUID } from 'node:crypto';
import * as bcrypt from 'bcrypt';

@Injectable()
export class Database implements OnModuleInit, OnModuleDestroy {
  readonly pool = new Pool({ connectionString: process.env.DATABASE_URL });
  async onModuleInit() {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      await client.query('SELECT pg_advisory_xact_lock(1062026)');
      await client.query(await readFile(resolve(__dirname, '../database/schema.sql'), 'utf8'));
      const email = process.env.OWNER_EMAIL?.trim().toLowerCase();
      const password = process.env.OWNER_PASSWORD;
      if (email && password) {
        if (password.length < 12 || Buffer.byteLength(password) > 72) throw new Error('OWNER_PASSWORD phải dài ít nhất 12 ký tự và tối đa 72 byte.');
        const existing = await client.query('SELECT id FROM owners WHERE email=$1', [email]);
        if (!existing.rowCount) await client.query('INSERT INTO owners(id,email,password_hash) VALUES($1,$2,$3)', [randomUUID(), email, await bcrypt.hash(password, 12)]);
      }
      await client.query('COMMIT');
    } catch (error) { await client.query('ROLLBACK'); throw error; }
    finally { client.release(); }
  }
  async onModuleDestroy() { await this.pool.end(); }
}
