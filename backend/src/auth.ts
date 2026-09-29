import { Body, CanActivate, Controller, ExecutionContext, ForbiddenException, Injectable, Post, UnauthorizedException } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { Throttle } from '@nestjs/throttler';
import * as bcrypt from 'bcrypt';
import { Database } from './database';
import { LoginDto } from './dto';

@Controller('auth')
export class AuthController {
  constructor(private readonly db: Database, private readonly jwt: JwtService) {}
  @Post('login')
  @Throttle({ default: { limit: 5, ttl: 60000 } })
  async login(@Body() dto: LoginDto) {
    const { rows } = await this.db.pool.query('SELECT * FROM owners WHERE email=$1', [dto.email]);
    const user = rows[0];
    // Perform a bcrypt comparison even for unknown accounts to reduce timing differences.
    const hash = user?.password_hash ?? '$2b$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW';
    const valid = await bcrypt.compare(dto.password, hash);
    if (!user || !valid) throw new UnauthorizedException('Email hoặc mật khẩu không đúng.');
    if (user.role !== 'owner') throw new ForbiddenException('Chỉ Chủ tiệm được quản lý nhóm dịch vụ.');
    return { accessToken: await this.jwt.signAsync({ sub: user.id, role: user.role }), user: { email: user.email, role: user.role } };
  }
}
@Injectable()
export class OwnerGuard implements CanActivate {
  constructor(private readonly jwt: JwtService, private readonly db: Database) {}
  async canActivate(context: ExecutionContext) {
    const req = context.switchToHttp().getRequest();
    const authorization = req.headers.authorization;
    if (typeof authorization !== 'string' || !authorization.startsWith('Bearer ')) throw new UnauthorizedException('Vui lòng đăng nhập.');
    let payload: { sub: string; role: string };
    try { payload = await this.jwt.verifyAsync(authorization.slice(7)); }
    catch { throw new UnauthorizedException('Phiên đăng nhập đã hết hạn.'); }
    if (payload.role !== 'owner') throw new ForbiddenException('Chỉ Chủ tiệm được thực hiện thao tác này.');
    const { rows } = await this.db.pool.query('SELECT role FROM owners WHERE id::text=$1', [payload.sub]);
    if (rows[0]?.role !== 'owner') throw new ForbiddenException('Tài khoản không có quyền Chủ tiệm.');
    return true;
  }
}
