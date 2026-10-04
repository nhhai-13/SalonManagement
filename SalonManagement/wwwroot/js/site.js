(() => {
    const menuButton = document.querySelector(".home-menu-toggle");
    const menu = document.querySelector(".home-nav");
    menuButton?.addEventListener("click", () => {
        const open = menu?.classList.toggle("open") === true;
        menuButton.setAttribute("aria-expanded", open.toString());
        menuButton.querySelector("i")?.classList.toggle("bi-list", !open);
        menuButton.querySelector("i")?.classList.toggle("bi-x-lg", open);
    });
    menu?.querySelectorAll("a").forEach(link => link.addEventListener("click", () => {
        menu.classList.remove("open");
        menuButton?.setAttribute("aria-expanded", "false");
    }));

    const bookingForm = document.getElementById("quick-booking-form");
    bookingForm?.addEventListener("submit", event => {
        event.preventDefault();
        const phone = document.getElementById("booking-phone");
        const message = document.getElementById("booking-message");
        if (!phone.checkValidity()) {
            message.textContent = "Vui lòng nhập số điện thoại Việt Nam gồm 10 chữ số.";
            message.classList.add("show");
            phone.focus();
            return;
        }
        bookingForm.submit();

    });
})();
