const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(
    path.join(__dirname, "../SalonManagement/wwwroot/js/customer-search.js"),
    "utf8"
);

class FakeElement {
    constructor(value = "") {
        this.value = value;
        this.textContent = "";
        this.listeners = {};
        this.children = [];
        this.classes = new Set(["d-none"]);
        this.classList = {
            add: name => this.classes.add(name),
            remove: name => this.classes.delete(name)
        };
    }
    addEventListener(name, handler) { this.listeners[name] = handler; }
    append(...items) { this.children.push(...items); }
    replaceChildren(...items) { this.children = [...items]; }
}

async function createPage(response) {
    const elements = Object.fromEntries([
        "customer-search-page", "customer-search-form", "customer-search-query",
        "customer-search-logout", "customer-search-status", "customer-search-table", "customer-search-results"
    ].map(id => [id, new FakeElement()]));
    const calls = [];
    const context = {
        window: {},
        document: { getElementById: id => elements[id], createElement: tag => Object.assign(new FakeElement(), { tag }) },
        SalonAuth: {
            authenticatedFetch: async (...args) => { calls.push(args); return response; },
            redirectToLogin() { throw new Error("unexpected redirect"); }
        },
        fetch: async () => ({ ok: true }),
        localStorage: { getItem: () => null },
        sessionStorage: { getItem: () => null },
        encodeURIComponent,
        setTimeout,
        clearTimeout
    };
    vm.runInNewContext(source, context);
    await context.window.ReceptionCustomerSearchPage.initialize();
    return { elements, calls, page: context.window.ReceptionCustomerSearchPage };
}

test("customer search requests the query and displays a profile link", async () => {
    const page = await createPage({ ok: true, status: 200, json: async () => ({
        results: [{ customerId: 12, fullName: "Nguyễn Ánh", phone: "0901234567", email: "anh@example.com", visitCount: 4 }],
        totalCount: 1,
        hasMore: false
    }) });
    page.elements["customer-search-query"].value = "nguyen anh";

    await page.elements["customer-search-form"].listeners.submit({ preventDefault() {} });

    assert.equal(page.calls[0][0], "/api/reception/customers?query=nguyen%20anh");
    assert.equal(page.elements["customer-search-status"].textContent, "Tìm thấy 1 hồ sơ.");
    const row = page.elements["customer-search-results"].children[0];
    assert.equal(row.children[0].textContent, "Nguyễn Ánh");
    assert.equal(row.children[4].children[0].href, "/reception/customers/12");
});

test("customer search shows empty and API error states", async () => {
    const empty = await createPage({ ok: true, status: 200, json: async () => ({ results: [], totalCount: 0, hasMore: false }) });
    empty.elements["customer-search-query"].value = "khong co";
    await empty.page.search();
    assert.equal(empty.elements["customer-search-status"].textContent, "Không tìm thấy hồ sơ phù hợp.");

    const failed = await createPage({ ok: false, status: 500, json: async () => ({ message: "Lỗi truy vấn" }) });
    failed.elements["customer-search-query"].value = "nguyen";
    await failed.page.search();
    assert.equal(failed.elements["customer-search-status"].textContent, "Lỗi truy vấn");
    assert.ok(failed.elements["customer-search-status"].className.includes("text-danger"));
});
