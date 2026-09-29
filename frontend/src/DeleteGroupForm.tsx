import { useEffect, useRef, useState } from 'react';
import { RefreshCw, Trash2 } from 'lucide-react';

type Check = { id: string; name: string; serviceCount: number; activeServiceCount: number; canDelete: boolean };
type Api = (path: string, options?: RequestInit) => Promise<any>;
type Props = { id: string; api: Api; busy: boolean; setBusy: (busy: boolean) => void; onClose: () => void; onDeleted: () => void };

export function DeleteGroupForm({ id, api, busy, setBusy, onClose, onDeleted }: Props) {
  const [check, setCheck] = useState<Check | null>(null);
  const [checking, setChecking] = useState(true);
  const [error, setError] = useState('');
  const reload = useRef<() => void>(() => undefined);
  const version = useRef(0);
  useEffect(() => {
    let disposed = false, running = false, queued = false;
    const load = async () => {
      if (disposed) return;
      if (running) { queued = true; return; }
      running = true;
      const requestVersion = ++version.current;
      setChecking(true);
      try {
        const latest = await api(`/service-groups/${id}/deletion-check`, { cache: 'no-store', signal: AbortSignal.timeout(10000) });
        if (!disposed && requestVersion === version.current) { setCheck(latest); setError(''); }
      } catch (e) {
        if (!disposed && requestVersion === version.current) {
          setCheck(null);
          setError((e as Error).name === 'TypeError' || (e as Error).name === 'TimeoutError' ? 'Không kiểm tra được số dịch vụ. Vui lòng thử lại.' : (e as Error).message);
        }
      } finally {
        running = false;
        if (!disposed) setChecking(false);
        if (queued && !disposed) { queued = false; void load(); }
      }
    };
    reload.current = () => { void load(); };
    void load();
    const stream = new EventSource('/api/public/catalog-events');
    stream.addEventListener('catalog-changed', reload.current);
    const focus = () => { if (document.visibilityState === 'visible') void load(); };
    window.addEventListener('focus', focus);
    const fallback = window.setInterval(focus, 30000);
    return () => { disposed = true; ++version.current; stream.close(); window.clearInterval(fallback); window.removeEventListener('focus', focus); };
  }, [id, api]);

  async function remove() {
    if (!check?.canDelete || checking || busy || error) return;
    ++version.current;
    setBusy(true); setError('');
    try { await api(`/service-groups/${id}`, { method: 'DELETE' }); onDeleted(); }
    catch (e) {
      ++version.current;
      const failure = e as Error & { data?: Partial<Check> & { code?: string } };
      if (failure.data?.code === 'GROUP_HAS_ACTIVE_SERVICES') {
        setCheck(previous => previous ? { ...previous, serviceCount: failure.data!.serviceCount!, activeServiceCount: failure.data!.activeServiceCount!, canDelete: false } : null);
      } else {
        setCheck(null);
        setError(failure.name === 'TypeError' ? 'Không kết nối được máy chủ. Hãy kiểm tra lại trước khi tiếp tục.' : failure.message);
      }
    } finally { setBusy(false); }
  }
  return <>
    <div className="delete-icon"><Trash2 size={25}/></div>
    {checking && !check && <p role="status">Đang kiểm tra số dịch vụ mới nhất…</p>}
    {check && <>
      <p>Nhóm <strong>“{check.name}”</strong> có tổng cộng <strong>{check.serviceCount} dịch vụ</strong>, trong đó <strong>{check.activeServiceCount} dịch vụ đang bán</strong>.</p>
      {!check.canDelete ? <div className="error" role="alert">Không thể xoá nhóm: còn {check.activeServiceCount} dịch vụ đang bán. Hãy chuyển các dịch vụ này sang nhóm khác hoặc ngừng bán trước khi xoá.</div>
        : <><p>Bạn có chắc muốn xoá nhóm này? Thao tác không thể hoàn tác.</p><p className="modal-description">{check.serviceCount ? `${check.serviceCount} dịch vụ ngừng bán được giữ lại và chuyển sang chưa phân nhóm.` : 'Nhóm sẽ được loại bỏ khỏi danh sách quản lý.'}</p></>}
    </>}
    {error && <div className="error" role="alert">{error}</div>}
    <div className="modal-actions" style={{ flexWrap: 'wrap' }}>
      <button className="secondary" disabled={busy || checking} onClick={() => reload.current()}><RefreshCw size={14}/>{checking ? 'Đang kiểm tra…' : 'Kiểm tra lại'}</button>
      <button className="secondary" disabled={busy} onClick={onClose}>Huỷ</button>
      <button className="danger" disabled={busy || checking || !check?.canDelete || !!error} onClick={() => void remove()}>{busy ? 'Đang xoá…' : 'Xác nhận xoá'}</button>
    </div>
  </>;
}
