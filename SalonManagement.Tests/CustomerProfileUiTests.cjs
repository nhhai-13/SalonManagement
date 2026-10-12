const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(
    path.join(__dirname, "../SalonManagement/wwwroot/js/customer-profile.js"),
    "utf8"
);

class FakeElement {
    constructor(value = "") {
        this.value = value;
        this.textContent = "";
        this.listeners = {};
        this.dataset = { customerId: "17" };
        this.classes = new Set(["d-none"]);
        this.classList = {
            add: name => this.classes.add(name),
            remove: name => this.classes.delete(name)
        };
        this.disabled = false;
    }
    addEventListener(name, handler) { this.listeners[name] = handler; }
    focus() { this.focused = true; }
}

async function createPage() {
    const elements = {
        "customer-profile-page": new FakeElement(),
        "customer-profile-logout": new FakeElement(),
        "customer-profile-form": new FakeElement(),
        "customer-profile-name": new FakeElement(),
        "customer-profile-phone": new FakeElement(),
        "customer-profile-email": new FakeElement(),
        "customer-profile-notes": new FakeElement(),
        "customer-profile-visits": new FakeElement(),
        "customer-profile-no-shows": new FakeElement(),
        "customer-profile-updated-at": new FakeElement(),
        "customer-profile-loading": new FakeElement(),
        "customer-profile-content": new FakeElement(),
        "customer-profile-error": new FakeElement(),
        "customer-profile-save": new FakeElement(),
        "customer-profile-result": new FakeElement()
    };
    const profile = {
        customerId: 17,
        fullName: "Khách quen",
        phone: "0901234567",
        email: "old@example.com",
        visitCount: 3,
        noShowCount: 1,
        notes: "Lưu ý cũ",
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: null
    };
    const calls = [];
    const context = {
        window: {},
        document: { getElementById: id => elements[id] },
        SalonAuth: {
            authenticatedFetch: async (...args) => {
                calls.push(args);
                return { ok: true, status: 200, json: async () => calls.length === 1 ? profile : {
                    ...profile,
                    fullName: "Tên mới",
                    email: "new@example.com",
                    notes: "Ghi chú mới",
                    updatedAt: "2026-10-12T10:30:00Z"
                } };
            },
            redirectToLogin() { throw new Error("unexpected redirect"); }
        },
        fetch: async () => ({ ok: true }),
        localStorage: { getItem: () => null },
        sessionStorage: { getItem: () => null }
    };
    vm.runInNewContext(source, context);
    await context.window.ReceptionCustomerProfilePage.initialize();
    return { elements, calls };
}

test("profile page displays the returned fields and attendance counts", async () => {
    const page = await createPage();

    assert.equal(page.calls[0][0], "/api/reception/customers/17");
    assert.equal(page.elements["customer-profile-name"].value, "Khách quen");
    assert.equal(page.elements["customer-profile-phone"].textContent, "0901234567");
    assert.equal(page.elements["customer-profile-email"].value, "old@example.com");
    assert.equal(page.elements["customer-profile-visits"].textContent, "3");
    assert.equal(page.elements["customer-profile-no-shows"].textContent, "1");
    assert.equal(page.elements["customer-profile-notes"].value, "Lưu ý cũ");
    assert.equal(page.elements["customer-profile-content"].classes.has("d-none"), false);
});

test("profile page saves editable fields without sending a phone number", async () => {
    const page = await createPage();
    page.elements["customer-profile-name"].value = "Tên mới";
    page.elements["customer-profile-email"].value = "new@example.com";
    page.elements["customer-profile-notes"].value = "Ghi chú mới";

    await page.elements["customer-profile-form"].listeners.submit({ preventDefault() {} });

    assert.equal(page.calls[1][0], "/api/reception/customers/17");
    assert.equal(page.calls[1][1].method, "PUT");
    const body = JSON.parse(page.calls[1][1].body);
    assert.deepEqual(body, { fullName: "Tên mới", email: "new@example.com", notes: "Ghi chú mới" });
    assert.equal(Object.hasOwn(body, "phone"), false);
    assert.equal(page.elements["customer-profile-result"].textContent, "Đã lưu thay đổi hồ sơ khách.");
});
