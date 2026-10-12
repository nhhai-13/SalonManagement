const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(
    path.join(__dirname, "../SalonManagement/wwwroot/js/site.js"),
    "utf8"
);

class FakeElement {
    constructor() {
        this.value = "";
        this.textContent = "";
        this.disabled = false;
        this.listeners = {};
        this.classes = new Set();
        this.classList = {
            add: name => this.classes.add(name),
            remove: name => this.classes.delete(name)
        };
    }
    addEventListener(name, handler) { this.listeners[name] = handler; }
    checkValidity() { return this.value.length > 0; }
    focus() { this.focused = true; }
    reset() { this.resetCalled = true; }
    querySelector() { return this.button; }
}

function createPage(response) {
    const form = new FakeElement();
    form.button = new FakeElement();
    const name = new FakeElement(); name.value = "Khách thử";
    const phone = new FakeElement(); phone.value = "0901234567";
    const service = new FakeElement(); service.value = "4";
    const stylist = new FakeElement(); stylist.value = "2";
    const date = new FakeElement(); date.value = "2026-10-13";
    const time = new FakeElement(); time.value = "10:00";
    const message = new FakeElement();
    const backToTop = new FakeElement();
    const elements = {
        "quick-booking-form": form,
        "booking-name": name,
        "booking-phone": phone,
        "booking-service": service,
        "booking-stylist": stylist,
        "booking-date": date,
        "booking-time": time,
        "booking-message": message,
        "back-to-top": backToTop
    };
    const calls = [];
    const document = {
        querySelector: () => null,
        getElementById: id => elements[id]
    };
    vm.runInNewContext(source, {
        document,
        window: { addEventListener() {} },
        fetch: async (...args) => { calls.push(args); return response; }
    });
    return { form, name, phone, service, stylist, date, time, message, calls };
}

test("online booking creates an appointment linked to the existing customer", async () => {
    const page = createPage({ ok: true, json: async () => ({ isReturningCustomer: true, appointmentId: 12 }) });
    await page.form.listeners.submit({ preventDefault() {} });

    assert.match(page.message.textContent, /khách quen/);
    assert.equal(page.calls.length, 1);
    assert.equal(page.calls[0][0], "/api/bookings");
    assert.equal(JSON.parse(page.calls[0][1].body).phone, "0901234567");
    assert.equal(JSON.parse(page.calls[0][1].body).serviceId, 4);
    assert.equal(JSON.parse(page.calls[0][1].body).stylistId, 2);
    assert.equal(JSON.parse(page.calls[0][1].body).startTime, "10:00:00");
});

test("online booking creates an appointment and new profile", async () => {
    const page = createPage({ ok: true, json: async () => ({ isReturningCustomer: false, created: true, appointmentId: 13 }) });
    await page.form.listeners.submit({ preventDefault() {} });

    assert.match(page.message.textContent, /khách mới/);
    assert.equal(page.form.resetCalled, true);
});

test("online booking shows duplicate-profile conflict without clearing the form", async () => {
    const page = createPage({ ok: false, json: async () => ({ message: "Số điện thoại đang gắn với nhiều hồ sơ." }) });
    await page.form.listeners.submit({ preventDefault() {} });

    assert.match(page.message.textContent, /nhiều hồ sơ/);
    assert.notEqual(page.form.resetCalled, true);
});
