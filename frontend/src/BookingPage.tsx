import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowRight, Flower2, Layers3, Leaf, RefreshCw, Search, Sparkles, X } from 'lucide-react';
import './booking.css';

type PublicGroup = { id: string; name: string; displayOrder: number; services: { id: string; name: string }[] };
type PublicStylist = { id: string; name: string };

export function BookingPage() {
  const [groups, setGroups] = useState<PublicGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [selectedServices, setSelectedServices] = useState<string[]>([]);
  const [stylists, setStylists] = useState<PublicStylist[]>([]);
  const [stylistLoading, setStylistLoading] = useState(false);
  const [stylistError, setStylistError] = useState('');
  const [selectedStylist, setSelectedStylist] = useState('any');
  const reload = useRef<() => void>(() => undefined);

  useEffect(() => {
    document.title = 'Dịch vụ & đặt lịch · Salon Studio';
    let disposed = false;
    let running = false;
    let queued = false;
    let controller: AbortController | undefined;
    const load = async () => {
      if (disposed) return;
      if (running) { queued = true; return; }
      running = true;
      controller = new AbortController();
      const timeout = window.setTimeout(() => controller?.abort(), 10000);
      try {
        const response = await fetch('/api/public/service-groups', { cache: 'no-store', signal: controller.signal });
        if (!response.ok) throw new Error('Không tải được dịch vụ. Vui lòng thử lại.');
        const data: PublicGroup[] = await response.json();
        if (!disposed) { setGroups(data); setError(''); }
      } catch {
        if (!disposed) setError('Chưa thể cập nhật danh sách dịch vụ. Vui lòng kiểm tra kết nối và thử lại.');
      } finally {
        window.clearTimeout(timeout);
        running = false;
        if (!disposed) setLoading(false);
        if (queued && !disposed) { queued = false; void load(); }
      }
    };
    reload.current = () => { void load(); };
    void load();
    const events = new EventSource('/api/public/catalog-events');
    events.addEventListener('catalog-changed', reload.current);
    const onVisible = () => { if (document.visibilityState === 'visible') void load(); };
    window.addEventListener('focus', onVisible);
    window.addEventListener('online', onVisible);
    document.addEventListener('visibilitychange', onVisible);
    // Recovery if an intermediary drops event-stream connections without notifying the browser.
    const fallback = window.setInterval(onVisible, 30000);
    return () => {
      disposed = true;
      controller?.abort();
      events.close();
      window.clearInterval(fallback);
      window.removeEventListener('focus', onVisible);
      window.removeEventListener('online', onVisible);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);
  useEffect(() => {
    if (!selectedServices.length) { setStylists([]); setStylistError(''); setStylistLoading(false); setSelectedStylist('any'); return; }
    const controller = new AbortController();
    setStylistLoading(true); setStylistError('');
    const query = new URLSearchParams({ serviceIds: selectedServices.join(',') });
    fetch(`/api/public/stylists?${query}`, { cache: 'no-store', signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error('Chưa thể tải danh sách thợ phù hợp. Vui lòng thử lại.');
        return response.json() as Promise<PublicStylist[]>;
      })
      .then(data => { setStylists(data); setSelectedStylist('any'); })
      .catch(error => { if (error.name !== 'AbortError') setStylistError(error.message); })
      .finally(() => { if (!controller.signal.aborted) setStylistLoading(false); });
    return () => controller.abort();
  }, [selectedServices]);
  const retry = useCallback(() => reload.current(), []);
  const term = search.trim().normalize('NFC').toLocaleLowerCase('vi');
  const visible = groups.map(group => ({ ...group, services: group.name.toLocaleLowerCase('vi').includes(term)
    ? group.services : group.services.filter(service => service.name.toLocaleLowerCase('vi').includes(term)) })).filter(group => group.services.length);
  const total = groups.reduce((sum, group) => sum + group.services.length, 0);

  return <div className="booking-page">
    <header className="booking-header"><a className="brand" href="/dat-lich" aria-label="Salon Studio — trang đặt lịch"><span className="brand-icon"><Sparkles size={22}/></span><span>salon<span className="brand-light">studio</span></span></a><a className="owner-link" href="/">Dành cho Chủ tiệm <ArrowRight size={15}/></a></header>
    <main>
      <section className="booking-hero">
        <div className="hero-copy"><div className="eyebrow">DỊCH VỤ & ĐẶT LỊCH</div><h1>Một chút chăm sóc,<br/><em>thêm nhiều rạng rỡ.</em></h1><p>Khám phá các dịch vụ của tiệm, được sắp xếp theo từng nhóm để bạn dễ dàng tìm thấy điều mình cần.</p><a className="hero-link" href="#dich-vu">Khám phá dịch vụ <ArrowDown size={17}/></a></div>
        <div className="hero-art" aria-hidden="true"><div className="art-orbit orbit-one"/><div className="art-orbit orbit-two"/><div className="art-arch"><Flower2 size={115} strokeWidth={.8}/><span>Thời gian dành cho bạn</span></div><div className="art-leaf"><Leaf size={38} strokeWidth={1}/></div><Sparkles className="art-sparkle" size={30} strokeWidth={1}/><span className="art-caption">CARE · BEAUTY · YOU</span></div>
      </section>

      <section className="public-catalog" id="dich-vu" aria-labelledby="catalog-title">
        <div className="catalog-heading"><div><div className="eyebrow">DANH MỤC CỦA TIỆM</div><h2 id="catalog-title">Dịch vụ dành cho bạn</h2><p>{loading ? 'Đang chuẩn bị danh sách dịch vụ…' : `${groups.length} nhóm · ${total} dịch vụ đang phục vụ`}</p></div><div className="booking-search"><Search size={18}/><input aria-label="Tìm nhóm hoặc dịch vụ" value={search} onChange={e => setSearch(e.target.value)} placeholder="Tìm nhóm hoặc dịch vụ…"/>{search && <button className="icon-button" aria-label="Xoá tìm kiếm" onClick={() => setSearch('')}><X size={16}/></button>}</div></div>
        {error && <div className="catalog-error" role="alert"><span>{error} {groups.length > 0 && 'Danh sách đang hiển thị có thể chưa phải phiên bản mới nhất.'}</span><button className="secondary" onClick={retry}><RefreshCw size={15}/> Thử lại</button></div>}
        {loading ? <div className="catalog-state" role="status"><Layers3 size={32}/><p>Đang tải dịch vụ…</p></div> : groups.length === 0 ? <div className="catalog-state"><Flower2 size={44} strokeWidth={1}/><h3>{error ? 'Danh sách dịch vụ chưa sẵn sàng' : 'Tiệm đang chuẩn bị những dịch vụ mới'}</h3><p>{error ? 'Bạn có thể thử tải lại danh sách.' : 'Hiện chưa có dịch vụ đang bán. Mời bạn quay lại sau nhé.'}</p></div> : visible.length === 0 ? <div className="catalog-state"><Search size={32}/><h3>Chưa tìm thấy dịch vụ phù hợp</h3><p>Thử một tên nhóm hoặc tên dịch vụ khác.</p><button className="secondary" onClick={() => setSearch('')}>Xem tất cả dịch vụ</button></div> : <div className="catalog-layout">
          <nav className="category-nav" aria-label="Nhóm dịch vụ"><span>KHÁM PHÁ THEO NHÓM</span>{visible.map(group => <a key={group.id} href={`#nhom-${group.id}`}><span>{group.name}</span><small>{group.services.length}</small></a>)}<div className="catalog-aside-note"><Leaf size={23} strokeWidth={1.2}/><p>Mỗi dịch vụ,<br/>một chút nâng niu.</p></div></nav>
          <div className="public-groups">{visible.map((group, index) => <section className="public-group" key={group.id} id={`nhom-${group.id}`} aria-labelledby={`title-${group.id}`}><header><div className={`public-group-icon tone-${index % 3}`}><Flower2 size={24} strokeWidth={1.4}/></div><div><h3 id={`title-${group.id}`}>{group.name}</h3><p>{group.services.length} dịch vụ</p></div><span className="group-sequence">{String(index + 1).padStart(2, '0')}</span></header><ul className="public-services">{group.services.map(service => { const chosen = selectedServices.includes(service.id); return <li key={service.id}><button type="button" className={`service-select ${chosen ? 'selected' : ''}`} aria-pressed={chosen} onClick={() => setSelectedServices(current => chosen ? current.filter(id => id !== service.id) : [...current, service.id])}><span className="service-dot"/><span>{service.name}</span><span className="service-available">{chosen ? 'Đã chọn' : 'Chọn dịch vụ'}</span></button></li>; })}</ul></section>)}
            <section className="stylist-picker" aria-labelledby="stylist-title"><div className="eyebrow">BƯỚC TIẾP THEO</div><h2 id="stylist-title">Chọn thợ phù hợp</h2><p>{selectedServices.length ? `Đã chọn ${selectedServices.length} dịch vụ. Danh sách chỉ gồm thợ làm được tất cả dịch vụ bạn chọn.` : 'Chọn dịch vụ phía trên để xem thợ có thể thực hiện.'}</p>
              {selectedServices.length > 0 && <>{stylistError && <div className="catalog-error" role="alert">{stylistError}</div>}{stylistLoading ? <div role="status" className="stylist-state">Đang tìm thợ phù hợp…</div> : stylistError ? null : stylists.length === 0 ? <div className="stylist-state" role="status">Chưa có thợ nào có thể thực hiện toàn bộ dịch vụ đã chọn. Bạn có thể bỏ bớt dịch vụ hoặc chọn lại sau.</div> : <div className="stylist-options" role="group" aria-label="Chọn thợ"><button type="button" aria-pressed={selectedStylist === 'any'} className={selectedStylist === 'any' ? 'stylist-option chosen' : 'stylist-option'} onClick={() => setSelectedStylist('any')}><strong>Thợ bất kỳ</strong><span>Để tiệm sắp xếp thợ phù hợp</span></button>{stylists.map(stylist => <button type="button" aria-pressed={selectedStylist === stylist.id} className={selectedStylist === stylist.id ? 'stylist-option chosen' : 'stylist-option'} key={stylist.id} onClick={() => setSelectedStylist(stylist.id)}><strong>{stylist.name}</strong><span>Có thể thực hiện toàn bộ dịch vụ</span></button>)}</div>}</>}
            </section>
          </div>
        </div>}
      </section>
    </main>
    <footer className="booking-footer"><span>Salon Studio</span><p>Chăm chút từng dịch vụ. Nâng niu từng trải nghiệm.</p><a href="#dich-vu">Xem dịch vụ <ArrowRight size={14}/></a></footer>
  </div>;
}
