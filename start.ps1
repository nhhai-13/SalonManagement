$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Test-Path 'backend/.env')) { throw 'Hãy copy backend/.env.example thành backend/.env và đặt tài khoản, JWT_SECRET trước khi chạy.' }
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$nodePath = if ($nodeCommand) { $nodeCommand.Source } else { Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' }
if (-not (Test-Path $nodePath)) { throw 'Cần Node.js 22+ để chạy dự án.' }
$env:Path = (Split-Path $nodePath) + ';' + $env:Path
$pnpmCommand = Get-Command pnpm -ErrorAction SilentlyContinue
if ($pnpmCommand) { & $pnpmCommand.Source run dev }
else {
  $bundledPnpm = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pnpm/bin/pnpm.cjs'
  if (-not (Test-Path $bundledPnpm)) { throw 'Cần cài pnpm 11+ hoặc dùng runtime Codex đã có trên máy.' }
  & $nodePath $bundledPnpm run dev
}
