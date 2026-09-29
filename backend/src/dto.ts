import { Transform } from 'class-transformer';
import { IsEmail, IsInt, IsString, Length, Max, Min, MaxLength } from 'class-validator';

export class GroupDto {
  @Transform(({ value }) => typeof value === 'string' ? value.normalize('NFC').trim().replace(/\s+/g, ' ') : value)
  @IsString() @Length(1, 100, { message: 'Tên nhóm phải có từ 1 đến 100 ký tự.' })
  name!: string;

  @IsInt({ message: 'Thứ tự hiển thị phải là số nguyên.' })
  @Min(0) @Max(9999)
  displayOrder!: number;
}
export class LoginDto {
  @Transform(({ value }) => typeof value === 'string' ? value.trim().toLowerCase() : value)
  @IsEmail({}, { message: 'Email không hợp lệ.' }) @MaxLength(254)
  email!: string;
  @IsString() @Length(1, 72)
  password!: string;
}
