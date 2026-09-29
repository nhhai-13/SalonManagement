const { test } = require('node:test');
const assert = require('node:assert/strict');
require('reflect-metadata');
const { plainToInstance } = require('class-transformer');
const { validate } = require('class-validator');
const { GroupDto } = require('../dist/dto');
const { GroupsService } = require('../dist/groups');

test('Normalize spaces and reject blank, oversized names and invalid orders', async () => {
  const valid = plainToInstance(GroupDto, {name:'  Chăm   sóc da  ', displayOrder:0});
  assert.equal(valid.name, 'Chăm sóc da');
  assert.equal((await validate(valid)).length, 0);
  for (const value of [{name:'  ',displayOrder:1},{name:'a'.repeat(101),displayOrder:1},{name:'Tóc',displayOrder:-1},{name:'Tóc',displayOrder:1.5},{name:'Tóc',displayOrder:'1'},{name:'Tóc',displayOrder:10000}]) {
    assert.ok((await validate(plainToInstance(GroupDto,value))).length > 0);
  }
});
test('Reject deletion and roll back when active services are present', async () => {
  const queries=[]; let released=false;
  const client={query:async sql=>{queries.push(sql); if(sql.startsWith('SELECT id')) return {rowCount:1}; if(sql.startsWith('SELECT is_active')) return {rows:[{is_active:true},{is_active:false},{is_active:true}]}; return {};},release:()=>released=true};
  const service=new GroupsService({pool:{connect:async()=>client}});
  await assert.rejects(service.remove('id'), /2 dịch vụ đang bán/);
  assert.ok(queries.includes('ROLLBACK')); assert.ok(!queries.some(q=>q.startsWith('DELETE'))); assert.ok(released);
});
