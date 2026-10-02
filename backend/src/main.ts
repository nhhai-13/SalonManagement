import 'reflect-metadata';
import 'dotenv/config';
import { Module, ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { JwtModule } from '@nestjs/jwt';
import { APP_GUARD } from '@nestjs/core';
import { ThrottlerGuard, ThrottlerModule } from '@nestjs/throttler';
import { Database } from './database';
import { AuthController, OwnerGuard } from './auth';
import { GroupsController, GroupsService } from './groups';
import { CatalogController, CatalogService } from './catalog';
import { StylistsController, StylistsService } from './stylists';

@Module({
  imports: [JwtModule.registerAsync({ useFactory: () => ({ secret: process.env.JWT_SECRET, signOptions: { expiresIn: '8h', issuer: 'salon-api', audience: 'salon-owner' }, verifyOptions: { issuer: 'salon-api', audience: 'salon-owner', algorithms: ['HS256'] } }) }), ThrottlerModule.forRoot([{ ttl: 60000, limit: 120 }])],
  controllers: [AuthController, GroupsController, CatalogController, StylistsController],
  providers: [Database, GroupsService, OwnerGuard, CatalogService, StylistsService, { provide: APP_GUARD, useClass: ThrottlerGuard }]
})
class AppModule {}
export async function createApp() {
  if (!process.env.DATABASE_URL) throw new Error('Thiếu DATABASE_URL trong backend/.env');
  if (!process.env.JWT_SECRET || process.env.JWT_SECRET.length < 32 || process.env.JWT_SECRET.startsWith('replace-')) throw new Error('JWT_SECRET phải là chuỗi riêng có ít nhất 32 ký tự.');
  const app = await NestFactory.create(AppModule);
  app.setGlobalPrefix('api');
  app.enableCors({ origin: process.env.FRONTEND_ORIGIN ?? 'http://localhost:5173' });
  app.useGlobalPipes(new ValidationPipe({ transform: true, whitelist: true, forbidNonWhitelisted: true }));
  app.enableShutdownHooks();
  return app;
}
if (require.main === module) createApp().then(app => app.listen(Number(process.env.PORT ?? 3000), '127.0.0.1')).catch(error => { console.error(error); process.exit(1); });
