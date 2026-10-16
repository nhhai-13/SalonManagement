// SCRUM-139: attach this to a counter-booking form.
// Required element ids: date, time, counter-form, message.
(() => {
  const form = document.getElementById('counter-form');
  const date = document.getElementById('date');
  const time = document.getElementById('time');
  if (!form || !date || !time) return;

  // Counter bookings may start now; do not apply the public 60-minute rule.
  if (!time.value) time.value = new Date().toTimeString().slice(0, 5);

  window.createImmediateCounterBooking = async body => {
    const response = await fetch('/reception/counter-appointments/create', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...body, date: date.value, startTime: `${time.value}:00` })
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || 'Không thể tạo lịch tại quầy.');
    return result;
  };
})();
