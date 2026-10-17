window.EmailSenderSettings = (() => {
  const byId = id => document.getElementById(id);
  const message = (text, ok) => { const target = byId("email-settings-message"); target.textContent = text; target.className = `small mt-3 mb-0 ${ok ? "text-success" : "text-danger"}`; };
  async function load() {
    const response = await SalonAuth.authenticatedFetch("/api/owner/email-settings");
    const status = await response.json().catch(() => ({}));
    const target = byId("email-settings-status");
    if (!response.ok) { target.className = "alert alert-danger"; target.textContent = "Không thể kiểm tra cấu hình email."; return; }
    if (status.isConfigured) { target.className = "alert alert-success"; target.textContent = `Email gửi đang sẵn sàng: ${status.senderEmail} (${status.server}:${status.port}).`; byId("smtp-server").value = status.server || "smtp.gmail.com"; byId("smtp-port").value = status.port || 587; byId("smtp-sender").value = status.senderEmail || ""; byId("smtp-username").value = status.username || ""; }
    else { target.className = "alert alert-warning"; target.textContent = "Chưa cấu hình email gửi. Vui lòng nhập Gmail và Mật khẩu ứng dụng."; }
  }
  return { start() {
    byId("email-settings-form").addEventListener("submit", async event => { event.preventDefault(); const button = byId("save-email-settings"); button.disabled = true; const response = await SalonAuth.authenticatedFetch("/api/owner/email-settings", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ server: byId("smtp-server").value, port: Number(byId("smtp-port").value), senderEmail: byId("smtp-sender").value, username: byId("smtp-username").value, appPassword: byId("smtp-password").value }) }); const result = await response.json().catch(() => ({})); button.disabled = false; if (!response.ok) return message(result.error || "Không thể lưu cấu hình.", false); byId("smtp-password").value = ""; message(result.message, true); load(); });
    byId("send-test-email-form").addEventListener("submit", async event => { event.preventDefault(); const button = byId("send-test-email"); button.disabled = true; const response = await SalonAuth.authenticatedFetch("/api/owner/email-settings/send-test", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ recipientEmail: byId("test-recipient").value }) }); const result = await response.json().catch(() => ({})); button.disabled = false; message(response.ok ? result.message : (result.error || "Không thể gửi thư thử."), response.ok); }); load(); }
  };
})();
