import React, { FormEvent, useCallback, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { ArrowDownWideNarrow, ArrowRight, Check, ChevronRight, CircleHelp, FolderOpen, Layers3, LayoutGrid, LogOut, Pencil, Plus, Search, ShieldCheck, Sparkles, Trash2, X } from 'lucide-react';
import './style.css';
import { BookingPage } from './BookingPage';
import { DeleteGroupForm } from './DeleteGroupForm';

type Group = { id: string; name: string; displayOrder: number; serviceCount: number; activeServiceCount: number };
type Session = { accessToken: string; user: { email: string } };
function readSession(): Session | null { try { const s = JSON.parse(sessionStorage.getItem('salon-session') ?? 'null'); return s?.accessToken && s?.user?.email ? s : null; } catch { return null; } }
function App() {
  const [session, setSession] = useState<Session | null>(readSession);
  const [groups, setGroups] = useState<Group[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Group | 'new' | null>(null);
  const [deleting, setDeleting] = useState<Group | null>(null);
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState('');
  const [help, setHelp] = useState(false);
  const logout = useCallback(() => { sessionStorage.removeItem('salon-session'); setSession(null); setGroups([]); setEditing(null); setDeleting(null); setNotice(''); }, []);
  const api = useCallback(async (path: string, options: RequestInit = {}) => {
    const response = await fetch(`/api${path}`, { ...options, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${session?.accessToken ?? ''}`, ...options.headers } });
    const data = await response.json();
    if (!response.ok) {
      if (response.status === 401 && path !== '/auth/login') logout();
      throw Object.assign(new Error(response.status === 429 ? 'Bạn thao tác quá nhanh. Vui lòng thử lại sau một phút.' : Array.isArray(data.message) ? data.message.join(' ') : data.message ?? 'Có lỗi xảy ra. Vui lòng thử lại.'), { status: response.status, data });
    }
    return data;
  }, [session, logout]);
  const refresh = useCallback(async () => {
    setLoading(true); setError('');
    try { setGroups(await api('/service-groups')); }
    catch (e) { setError(e instanceof TypeError ? 'Không kết nối được máy chủ. Vui lòng kiểm tra kết nối rồi thử lại.' : (e as Error).message); }
    finally { setLoading(false); }
  }, [api]);
  useEffect(() => { if (session) void refresh(); }, [session, refresh]);
  useEffect(() => { if (notice) { const t = setTimeout(() => setNotice(''), 5000); return () => clearTimeout(t); } }, [notice]);
  async function login(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setError('');
    const data = new FormData(e.currentTarget);
    try { const s = await api('/auth/login', { method: 'POST', body: JSON.stringify({ email: data.get('email'), password: data.get('password') }) }); sessionStorage.setItem('salon-session', JSON.stringify(s)); setSession(s); }
    catch (e) { setError(e instanceof TypeError ? 'Không kết nối được máy chủ. Hãy kiểm tra backend đang chạy.' : (e as Error).message); }
    finally { setBusy(false); }
  }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setFormError('');
    const data = new FormData(e.currentTarget);
    const name = String(data.get('name') ?? '').trim().replace(/\s+/g, ' ');
    const displayOrder = Number(data.get('displayOrder'));
    if (!name || name.length > 100 || !Number.isInteger(displayOrder) || displayOrder < 0 || displayOrder > 9999) { setFormError('Nhập tên nhóm từ 1–100 ký tự và thứ tự nguyên từ 0–9999.'); setBusy(false); return; }
    const isNew = editing === 'new';
    try {
      await api(isNew ? '/service-groups' : `/service-groups/${(editing as Group).id}`, { method: isNew ? 'POST' : 'PUT', body: JSON.stringify({ name, displayOrder }) });
      setEditing(null); setNotice(isNew ? `Đã thêm nhóm “${name}”.` : `Đã cập nhật nhóm “${name}”.`); await refresh();
    } catch (e) { setFormError(e instanceof TypeError ? 'Không kết nối được máy chủ. Vui lòng thử lại.' : (e as Error).message); }
    finally { setBusy(false); }
  }
  const add = () => { setFormError(''); setEditing('new'); };
  if (!session) return <main className="login-page"><div className="login-brand"><span className="brand-icon"><Sparkles size={25}/></span> salon<span className="brand-light">studio</span></div><section className="login-card"><div className="eyebrow">KHÔNG GIAN DÀNH CHO CHỦ TIỆM</div><h1>Chào mừng trở lại.</h1><p>Đăng nhập để quản lý nhóm dịch vụ của tiệm bạn.</p><form onSubmit={login}><label>Email<input name="email" type="email" placeholder="owner@salon.local" autoComplete="username" required/></label><label>Mật khẩu<input name="password" type="password" autoComplete="current-password" placeholder="Nhập mật khẩu của bạn" required maxLength={72}/></label>{error && <div className="error" role="alert">{error}</div>}<button className="primary login-submit" disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}<ArrowRight size={18}/></button></form><div className="login-note"><ShieldCheck size={16}/> Chỉ tài khoản Chủ tiệm được cấp quyền truy cập.</div></section><div className="login-footer">Chăm chút từng dịch vụ. Nâng niu từng trải nghiệm.</div></main>;
  const filtered = groups.filter(g => g.name.toLocaleLowerCase('vi').includes(search.trim().toLocaleLowerCase('vi')));
  return <div className="app-shell"><aside className="sidebar"><div className="brand"><span className="brand-icon"><Sparkles size={22}/></span><span>salon<span className="brand-light">studio</span></span></div><div className="workspace"><div className="workspace-icon">S</div><div><strong>Không gian của tiệm</strong><small>Quản lý salon</small></div><ChevronRight size={15}/></div><div className="nav-label">QUẢN LÝ</div><nav><a className="nav-active" href="#groups"><Layers3 size={19}/> Nhóm dịch vụ <span>{groups.length}</span></a></nav><div className="sidebar-bottom"><div className="tip-card"><Sparkles size={20}/><strong>Gọn gàng từ từng nhóm</strong><p>Phân loại rõ ràng để việc quản lý dịch vụ trở nên dễ dàng hơn.</p></div><button className="help-button" onClick={() => setHelp(true)}><CircleHelp size={18}/> Hướng dẫn sử dụng</button><div className="profile"><div className="avatar">CT</div><div><strong>Chủ tiệm</strong><small title={session.user.email}>{session.user.email}</small></div><button className="icon-button" aria-label="Đăng xuất" title="Đăng xuất" onClick={logout}><LogOut size={17}/></button></div></div></aside><div className="main-shell"><header className="topbar"><div>Quản lý <ChevronRight size={14}/><strong>Nhóm dịch vụ</strong></div><a className="owner-badge" href="/dat-lich" target="_blank" rel="noopener noreferrer">Xem trang đặt lịch <ArrowRight size={15}/></a></header><main className="content" id="groups"><div className="page-heading"><div><div className="eyebrow">DANH MỤC DỊCH VỤ</div><h1>Nhóm dịch vụ</h1><p>Sắp xếp dịch vụ khoa học, quản lý tiệm dễ dàng.</p></div><button className="primary" onClick={add}><Plus size={18}/> Thêm nhóm dịch vụ</button></div><div className="stats"><Stat icon={<Layers3/>} label="Tổng nhóm dịch vụ" value={loading ? '—' : groups.length} caption="Danh mục của tiệm"/><Stat icon={<LayoutGrid/>} label="Nhóm có dịch vụ" value={loading ? '—' : groups.filter(g=>g.serviceCount > 0).length} caption="Đã được phân loại"/><Stat icon={<FolderOpen/>} label="Nhóm chưa có dịch vụ" value={loading ? '—' : groups.filter(g=>g.serviceCount === 0).length} caption="Sẵn sàng để sử dụng"/></div><section className="list-card"><div className="list-heading"><div><h2>Danh sách nhóm <span>{groups.length}</span></h2><p>Tất cả nhóm dịch vụ, theo thứ tự bạn đã thiết lập.</p></div><div className="search"><Search size={17}/><input aria-label="Tìm nhóm dịch vụ" placeholder="Tìm tên nhóm dịch vụ…" value={search} onChange={e=>setSearch(e.target.value)}/>{search && <button className="icon-button" aria-label="Xoá tìm kiếm" onClick={()=>setSearch('')}><X size={14}/></button>}</div></div>{error ? <div className="list-state"><div className="error" role="alert">{error}</div><button className="secondary" onClick={()=>void refresh()}>Thử lại</button></div> : loading ? <div className="list-state" role="status">Đang tải nhóm dịch vụ…</div> : filtered.length === 0 ? <div className="list-state"><div className="empty-icon"><FolderOpen size={32}/></div><h3>{search ? 'Không tìm thấy nhóm phù hợp' : 'Bắt đầu với nhóm dịch vụ đầu tiên'}</h3><p>{search ? 'Thử tìm kiếm bằng một tên nhóm khác.' : 'Tạo nhóm như Tóc, Gội dưỡng hoặc Chăm sóc da để phân loại dịch vụ.'}</p>{!search && <button className="primary" onClick={add}><Plus size={17}/> Thêm nhóm dịch vụ</button>}</div> : <div className="table-scroll"><table><thead><tr><th className="order-col"><ArrowDownWideNarrow size={14}/> Thứ tự</th><th>Tên nhóm dịch vụ</th><th>Dịch vụ</th><th>Trạng thái</th><th className="actions-col">Thao tác</th></tr></thead><tbody>{filtered.map((g, i)=><tr key={g.id}><td><span className="order-number">{String(g.displayOrder).padStart(2,'0')}</span></td><td><div className="group-name"><span className={`group-icon color-${i%3}`}><FolderOpen size={21}/></span><strong>{g.name}</strong></div></td><td><span className="service-count">{g.serviceCount}</span><span className="muted"> dịch vụ</span></td><td><span className={`status ${g.activeServiceCount ? 'selling' : 'empty'}`}><i/>{g.activeServiceCount ? `${g.activeServiceCount} đang bán` : g.serviceCount ? 'Ngừng bán' : 'Nhóm trống'}</span></td><td><div className="row-actions"><button className="icon-button" title="Sửa nhóm" aria-label={`Sửa ${g.name}`} onClick={()=>{setFormError('');setEditing(g);}}><Pencil size={17}/></button><button className="icon-button danger-icon" title="Xoá nhóm" aria-label={`Xoá ${g.name}`} onClick={()=>{setFormError('');setDeleting(g);}}><Trash2 size={17}/></button></div></td></tr>)}</tbody></table></div>}<div className="table-footer"><span>Hiển thị {filtered.length} / {groups.length} nhóm dịch vụ</span><span><ArrowDownWideNarrow size={14}/> Thứ tự tăng dần</span></div></section><div className="info-note"><CircleHelp size={18}/><p><strong>Một danh mục rõ ràng, một trải nghiệm tốt hơn.</strong> Thứ tự nhỏ hơn được hiển thị trước. Chỉ xoá được nhóm không còn dịch vụ đang bán.</p></div><footer className="page-footer">Salon Studio <span>Không gian quản lý dành riêng cho tiệm của bạn</span></footer></main></div>{notice && <div className="toast" role="status"><Check size={18}/>{notice}<button className="icon-button" aria-label="Đóng thông báo" onClick={()=>setNotice('')}><X size={15}/></button></div>}
  {editing && <Modal title={editing === 'new' ? 'Thêm nhóm dịch vụ' : 'Sửa nhóm dịch vụ'} onClose={()=>!busy && setEditing(null)}><p className="modal-description">Đặt tên dễ nhận biết và chọn vị trí cho nhóm trong danh sách.</p><form onSubmit={save}><label>Tên nhóm <span className="required">*</span><input name="name" autoFocus required maxLength={100} defaultValue={editing==='new'?'':editing.name} placeholder="Ví dụ: Chăm sóc da"/><small>Từ 1–100 ký tự, không trùng tên nhóm đã có.</small></label><label>Thứ tự hiển thị <span className="required">*</span><input name="displayOrder" type="number" min={0} max={9999} step={1} required defaultValue={editing==='new'?Math.min(9999,groups.length ? Math.max(...groups.map(g=>g.displayOrder))+1 : 1):editing.displayOrder}/><small>Số nguyên từ 0–9999. Số nhỏ hơn hiển thị trước.</small></label>{formError && <div className="error" role="alert">{formError}</div>}<div className="modal-actions"><button type="button" className="secondary" disabled={busy} onClick={()=>setEditing(null)}>Huỷ</button><button className="primary" disabled={busy}>{busy?'Đang lưu…':editing==='new'?'Tạo nhóm':'Lưu thay đổi'}</button></div></form></Modal>}
  {deleting && <Modal title="Xoá nhóm dịch vụ?" onClose={()=>{if(!busy){setDeleting(null);void refresh();}}}><DeleteGroupForm id={deleting.id} api={api} busy={busy} setBusy={setBusy} onClose={()=>{setDeleting(null);void refresh();}} onDeleted={()=>{setNotice(`Đã xoá nhóm “${deleting.name}”.`);setDeleting(null);void refresh();}}/></Modal>}
  {help && <Modal title="Quản lý nhóm dịch vụ" onClose={()=>setHelp(false)}><ol className="help-list"><li>Chọn <strong>Thêm nhóm dịch vụ</strong>, nhập tên và thứ tự rồi lưu.</li><li>Chọn biểu tượng bút để sửa tên hoặc thứ tự.</li><li>Chọn biểu tượng thùng rác và xác nhận để xoá nhóm.</li></ol><p>Tên nhóm không phân biệt chữ hoa/chữ thường khi kiểm tra trùng. Nhóm có cùng thứ tự được xếp theo thời gian tạo.</p><p>Nhóm còn dịch vụ đang bán sẽ bị chặn xoá. Dịch vụ ngừng bán được giữ lại khi xoá nhóm.</p><div className="modal-actions"><button className="primary" onClick={()=>setHelp(false)}>Đã hiểu</button></div></Modal>}</div>;
}
function Stat({icon,label,value,caption}:{icon:React.ReactNode;label:string;value:number|string;caption:string}){return <article className="stat"><div><span>{label}</span><strong>{value}</strong><small>{caption}</small></div><div className="stat-icon">{icon}</div></article>;}
function Modal({title,onClose,children}:{title:string;onClose:()=>void;children:React.ReactNode}) {
  const ref=useRef<HTMLDialogElement>(null);
  const closeRef=useRef(onClose); closeRef.current=onClose;
  useEffect(()=>{ const previous=document.activeElement as HTMLElement|null; const dialog=ref.current!; dialog.showModal(); const cancel=(e:Event)=>{e.preventDefault();closeRef.current();}; dialog.addEventListener('cancel',cancel); return ()=>{dialog.removeEventListener('cancel',cancel);dialog.close();previous?.focus();}; },[]);
  return <dialog ref={ref} className="modal" aria-labelledby="modal-title"><div className="modal-heading"><h2 id="modal-title">{title}</h2><button className="icon-button" aria-label="Đóng hộp thoại" onClick={onClose}><X size={20}/></button></div>{children}</dialog>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode>{window.location.pathname.replace(/\/+$/, '') === '/dat-lich' ? <BookingPage/> : <App/>}</React.StrictMode>);


