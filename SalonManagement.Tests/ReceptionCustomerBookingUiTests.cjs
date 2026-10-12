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
        this.classList = { remove() {} };
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

async function createPage(response) {
    const elements = {
        "booking-customer-logout": new FakeElement(),
        "reception-booking-customer-form": new FakeElement(),
        "reception-booking-customer-page": new FakeElement(),
        "reception-customer-name": new FakeElement("Khách thử"),
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
                        json: async () => ({ isReturningCustomer: true, profile: { customerId: 41, fullName: "Khách quen" } })
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

    assert.match(page.elements["reception-customer-result"].textContent, /khách quen/);
    assert.equal(page.calls[0][0], "/api/reception/bookings");
    assert.equal(JSON.parse(page.calls[0][1].body).phone, "+84 901-234-567");
    assert.equal(JSON.parse(page.calls[0][1].body).serviceId, 4);
    assert.equal(JSON.parse(page.calls[0][1].body).stylistId, 2);
    assert.equal(JSON.parse(page.calls[0][1].body).startTime, "10:00:00");
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
});

test("reception booking creates an appointment and new profile", async () => {
    const page = await createPage({ ok: true, status: 200, json: async () => ({ isReturningCustomer: false, created: true, appointmentId: 23 }) });
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });

    assert.match(page.elements["reception-customer-result"].textContent, /khách mới/);
});

test("reception booking shows a duplicate-phone conflict without clearing the form", async () => {
    const page = await createPage({ ok: false, status: 409, json: async () => ({ message: "Số điện thoại đang gắn với nhiều hồ sơ." }) });
    await page.elements["reception-booking-customer-form"].listeners.submit({ preventDefault() {} });

    assert.match(page.elements["reception-customer-result"].textContent, /nhiều hồ sơ/);
});
