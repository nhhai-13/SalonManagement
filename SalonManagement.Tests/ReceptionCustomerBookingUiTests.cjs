const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(
    path.join(__dirname, "../SalonManagement/wwwroot/js/reception-booking-customer.js"),
    "utf8"
);

class FakeElement {
    constructor(value = "") {
        this.value = value;
        this.listeners = {};
        this.classList = {
            remove: name => this.classes?.delete(name),
            add: name => this.classes?.add(name),
            toggle: (name, force) => force ? this.classes?.add(name) : this.classes?.delete(name)
        };
        this.classes = new Set(["d-none"]);
        this.disabled = false;
        this.options = [];
    }
    addEventListener(name, handler) { this.listeners[name] = handler; }
    replaceChildren(...items) { this.options = [...items]; }
    add(item) { this.options.push(item); }
    append(...items) {
        this.appended = [...(this.appended || []), ...items];
        this.textContent += items.filter(item => typeof item === "string").join("");
    }
}

async function createPage(response, lookupProfile = { customerId: 41, fullName: "Khách quen" }) {
    const elements = {
        "booking-customer-logout": new FakeElement(),
        "reception-booking-customer-form": new FakeElement(),
        "reception-booking-customer-page": new FakeElement(),
        "reception-customer-name": new FakeElement("Khách thử"),
        "reception-customer-name-warning": new FakeElement(),
        "reception-customer-profile-name": new FakeElement(),
        "reception-customer-appointment-name": new FakeElement(),
        "reception-customer-name-choice": new FakeElement(),
        "reception-customer-keep-profile-name": new FakeElement(),
        "reception-customer-update-profile-name": new FakeElement(),
        "reception-customer-phone": new FakeElement("+84 901-234-567"),
        "reception-customer-lookup": new FakeElement(),
        "reception-booking-service": new FakeElement("4"),
        "reception-booking-stylist": new FakeElement("2"),
        "reception-booking-date": new FakeElement("2026-10-13"),
        "reception-booking-time": new FakeElement("10:00"),
        "reception-customer-submit": new FakeElement(),
        "reception-customer-result": new FakeElement()
    };
    const calls = [];
    const fetchCalls = [];
    const context = {
        window: {},
        document: { getElementById: id => elements[id], createElement: () => new FakeElement() },
        SalonAuth: {
            authenticatedFetch: async (...args) => {
                calls.push(args);
                if (args[0].startsWith("/api/reception/customers/lookup")) {
                    return {
                        ok: true,
                        status: 200,
                        json: async () => lookupProfile
                            ? ({ isReturningCustomer: true, profile: lookupProfile })
                            : ({ isReturningCustomer: false, profile: null })
                    };
                }
                return response;
            },
            redirectToLogin() { throw new Error("unexpected redirect"); }
        },
        localStorage: { getItem: () => null },
        sessionStorage: { getItem: () => null },
        fetch: async (...args) => {
            fetchCalls.push(args);
            return { ok: true, json: async () => ({
                services: [{ id: 4, name: "Cắt tóc" }],
                stylists: [{ id: 2, name: "Thợ A" }]
            }) };
        },
        Option: function Option(text, value) { return { text, value }; },
        console
    };
    vm.runInNewContext(source, context);
    await context.window.ReceptionBookingCustomerPage.initialize();
    return { elements, calls, fetchCalls };
}

test("reception booking links to the existing customer profile", async () => {
    const page = await createPage({ ok: true, status: 200, json: async () => ({ isReturningCustomer: true, customerId: 8, appointmentId: 22 }) });
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });
    assert.match(page.elements["reception-customer-result"].textContent, /Vui lòng chọn/);
    page.elements["reception-customer-keep-profile-name"].listeners.click();
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });

    assert.match(page.elements["reception-customer-result"].textContent, /khách quen/);
    const booking = page.calls.find(call => call[0] === "/api/reception/bookings");
    assert.equal(JSON.parse(booking[1].body).phone, "+84 901-234-567");
    assert.equal(JSON.parse(booking[1].body).serviceId, 4);
    assert.equal(JSON.parse(booking[1].body).stylistId, 2);
    assert.equal(JSON.parse(booking[1].body).startTime, "10:00:00");
    assert.equal(JSON.parse(booking[1].body).updateCustomerProfileName, false);
    assert.equal(page.elements["reception-customer-result"].appended[1].href, "/reception/customers/8");
});

test("reception booking recognizes and links the customer profile before creating an appointment", async () => {
    const page = await createPage({ ok: true, status: 200, json: async () => ({ isReturningCustomer: true, customerId: 41, appointmentId: 22 }) });
    await page.elements["reception-customer-phone"].listeners.blur();

    const lookup = page.calls.find(call => call[0].startsWith("/api/reception/customers/lookup"));
    assert.ok(lookup);
    assert.match(lookup[0], /phone=%2B84%20901-234-567/);
    assert.match(page.elements["reception-customer-lookup"].textContent, /Khách quen/);
    assert.equal(page.elements["reception-customer-lookup"].appended[1].href, "/reception/customers/41");
    assert.equal(page.elements["reception-customer-name-warning"].classes.has("d-none"), false);
    assert.equal(page.elements["reception-customer-profile-name"].textContent, "Khách quen");
    assert.equal(page.elements["reception-customer-appointment-name"].textContent, "Khách thử");
});

test("matching name ignoring case and extra spaces does not show a warning", async () => {
    const page = await createPage(
        { ok: true, status: 200, json: async () => ({ isReturningCustomer: true, customerId: 41, appointmentId: 22 }) },
        { customerId: 41, fullName: "nguyễn  minh   an" });
    page.elements["reception-customer-name"].value = "  NGUYỄN MINH AN  ";
    await page.elements["reception-customer-phone"].listeners.blur();

    assert.equal(page.elements["reception-customer-name-warning"].classes.has("d-none"), true);
});

test("reception can explicitly confirm updating a returning customer's profile name", async () => {
    const page = await createPage({ ok: true, status: 200, json: async () => ({ isReturningCustomer: true, customerId: 41, appointmentId: 22 }) });
    await page.elements["reception-customer-phone"].listeners.blur();
    page.elements["reception-customer-update-profile-name"].listeners.click();
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });

    const call = page.calls.find(item => item[0] === "/api/reception/bookings");
    assert.equal(JSON.parse(call[1].body).updateCustomerProfileName, true);
    assert.match(page.elements["reception-customer-name-choice"].textContent, /sau khi lịch được lưu thành công/);
});

test("reception booking creates an appointment and new profile", async () => {
    const page = await createPage({ ok: true, status: 200, json: async () => ({ isReturningCustomer: false, created: true, appointmentId: 23 }) }, null);
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });

    assert.match(page.elements["reception-customer-result"].textContent, /khách mới/);
});

test("reception booking shows a duplicate-phone conflict without clearing the form", async () => {
    const page = await createPage({ ok: false, status: 409, json: async () => ({ message: "Số điện thoại đang gắn với nhiều hồ sơ." }) });
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });
    await page.elements["reception-customer-keep-profile-name"].listeners.click();
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });

    assert.match(page.elements["reception-customer-result"].textContent, /nhiều hồ sơ/);
});
