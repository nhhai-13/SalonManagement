const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(
    path.join(__dirname, "../SalonManagement/wwwroot/js/schedule-coordination.js"),
    "utf8"
);

class FakeElement {
    constructor() {
        this.value = "";
        this.disabled = false;
        this.textContent = "";
        this.listeners = {};
        this.children = [];
        this.options = [];
        const classes = new Set(["d-none"]);
        this.classList = {
            add: name => classes.add(name),
            remove: name => classes.delete(name),
            contains: name => classes.has(name),
            toggle: (name, force) => force ? classes.add(name) : classes.delete(name)
        };
    }
    addEventListener(name, handler) { this.listeners[name] = handler; }
    replaceChildren(...items) { this.children = items; }
    append(...items) { this.children.push(...items); }
    add(option) { this.options.push(option); }
    reportValidity() { return true; }
}

function createPage(appointmentResponse, transferResponses = {}) {
    const ids = [
        "schedule-coordination-filter", "stylist-id", "day-off", "load-appointments",
        "schedule-error", "schedule-loading", "schedule-empty", "schedule-results",
        "schedule-appointments", "schedule-count", "schedule-coordination-page", "logout",
        "transfer-selected-count", "preview-transfers", "transfer-preview",
        "transfer-preview-summary", "transfer-preview-list", "confirm-transfers",
        "cancel-transfer-preview", "transfer-results", "transfer-results-summary",
        "transfer-results-list"
    ];
    const elements = Object.fromEntries(ids.map(id => [id, new FakeElement()]));
    const requests = [];
    const document = {
        getElementById: id => elements[id],
        createElement: () => new FakeElement()
    };
    const auth = {
        authenticatedFetch: async (url, options) => {
            if (url.endsWith("/stylists")) {
                return { ok: true, status: 200, json: async () => [{ id: 4, name: "Thợ kiểm thử" }] };
            }
            if (url.endsWith("/transfers/preview")) {
                requests.push({ url, options });
                return transferResponses.preview;
            }
            if (url.endsWith("/transfers")) {
                requests.push({ url, options });
                return transferResponses.transfer;
            }
            return appointmentResponse;
        },
        redirectToLogin() {}
    };
    const window = {};
    vm.runInNewContext(source, {
        window,
        document,
        SalonAuth: auth,
        URLSearchParams,
        Option: function (text, value) { return { text, value }; }
    });
    return { page: window.ScheduleCoordinationPage, elements, requests };
}

test("shows a friendly error when the appointment list cannot be loaded", async () => {
    const { page, elements } = createPage({
        ok: false,
        status: 500,
        json: async () => ({ message: "Không thể tải lịch hẹn." })
    });

    await page.initialize();
    elements["stylist-id"].value = "4";
    elements["day-off"].value = "2026-10-11";
    await elements["schedule-coordination-filter"].listeners.submit({ preventDefault() {} });

    assert.equal(elements["schedule-error"].textContent, "Không thể tải lịch hẹn.");
    assert.equal(elements["schedule-error"].classList.contains("d-none"), false);
    assert.equal(elements["schedule-results"].classList.contains("d-none"), true);
});

test("shows the empty state when the selected stylist and date have no appointments", async () => {
    const { page, elements } = createPage({
        ok: true,
        status: 200,
        json: async () => ({ totalCount: 0, appointments: [] })
    });

    await page.initialize();
    elements["stylist-id"].value = "4";
    elements["day-off"].value = "2026-10-11";
    await elements["schedule-coordination-filter"].listeners.submit({ preventDefault() {} });

    assert.equal(elements["schedule-count"].textContent, "0 lịch cần xử lý");
    assert.equal(elements["schedule-empty"].classList.contains("d-none"), false);
});

test("renders suggested alternative times and the reason the original time is unavailable", async () => {
    const appointment = {
        id: 12,
        customerName: "Khách thử",
        services: ["Cắt tóc nam"],
        startTime: "09:00",
        durationMinutes: 60,
        status: "Confirmed",
        customerPhone: "0900000000",
        plan: {
            hasOptions: true,
            stylists: [{
                stylistId: 8,
                stylistName: "Thợ thay thế",
                availableAtOriginalTime: false,
                reasonsUnavailableAtOriginalTime: ["Bị trùng lịch với lịch hẹn #55."],
                suggestedStartTimes: ["10:00", "10:15"]
            }]
        }
    };
    const { page, elements } = createPage({
        ok: true,
        status: 200,
        json: async () => ({ totalCount: 1, appointments: [appointment] })
    });

    await page.initialize();
    elements["stylist-id"].value = "4";
    elements["day-off"].value = "2026-10-11";
    await elements["schedule-coordination-filter"].listeners.submit({ preventDefault() {} });

    const row = elements["schedule-appointments"].children[0];
    const option = row.children[7].children[0].children[0];
    assert.equal(option.children[1].textContent, "Có thể nhận lúc: 10:00, 10:15");
    assert.match(option.children[2].textContent, /Bị trùng lịch/);
});

test("previews a selected batch and renders per-appointment transfer results", async () => {
    const appointment = {
        id: 15,
        customerName: "Khách chuyển lịch",
        services: ["Cắt tóc nam"],
        startTime: "09:00",
        durationMinutes: 60,
        status: "Confirmed",
        plan: {
            hasOptions: true,
            stylists: [{
                stylistId: 8,
                stylistName: "Thợ thay thế",
                availableAtOriginalTime: true,
                reasonsUnavailableAtOriginalTime: [],
                suggestedStartTimes: []
            }]
        }
    };
    const { page, elements, requests } = createPage({
        ok: true,
        status: 200,
        json: async () => ({ totalCount: 1, appointments: [appointment] })
    }, {
        preview: { ok: true, status: 200, json: async () => ({
            successCount: 1,
            failureCount: 0,
            results: [{ appointmentId: 15, outcome: "Ready", message: "Phương án hợp lệ." }]
        }) },
        transfer: { ok: true, status: 200, json: async () => ({
            successCount: 1,
            failureCount: 0,
            results: [{ appointmentId: 15, outcome: "Succeeded", message: "Đã chuyển lịch thành công.",
                stylistId: 8, stylistName: "Thợ thay thế", startTime: "10:00", appointmentStatus: "Confirmed",
                emailStatus: "Pending", emailAttemptCount: 0 }]
        }) }
    });

    await page.initialize();
    elements["stylist-id"].value = "4";
    elements["day-off"].value = "2026-10-11";
    await elements["schedule-coordination-filter"].listeners.submit({ preventDefault() {} });
    const row = elements["schedule-appointments"].children[0];
    row.children[0].children[0].checked = true;
    row.children[8].children[0].value = row.children[8].children[0].options[1].value;
    row.children[9].children[0].checked = true;
    await elements["preview-transfers"].listeners.click();

    assert.equal(requests.length, 1);
    assert.equal(requests[0].url, "/api/schedule-coordination/transfers/preview");
    assert.equal(JSON.parse(requests[0].options.body).items[0].customerConfirmed, true);
    assert.equal(elements["transfer-preview"].classList.contains("d-none"), false);
    assert.equal(elements["confirm-transfers"].disabled, false);

    await elements["confirm-transfers"].listeners.click();

    assert.equal(requests.length, 2);
    assert.equal(requests[1].url, "/api/schedule-coordination/transfers");
    assert.match(elements["transfer-results-summary"].textContent, /1 lịch thành công/);
    assert.match(row.children[10].textContent, /Thợ: Thợ thay thế, giờ: 10:00, trạng thái: Confirmed/);
    assert.match(row.children[10].textContent, /Đã chuyển lịch thành công/);
    assert.match(row.children[10].children[0].textContent, /Email: đang chờ gửi/);
    assert.equal(row.children[0].children[0].disabled, true);
});
