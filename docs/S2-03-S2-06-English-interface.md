# English interface for S2-03 and S2-06

The public catalog, homepage featured-services section and booking/stylist selection flow now use English labels. This includes search, empty states, loading/error messages, any-stylist selection, assignment confirmation, duration units and estimated-price notes. Currency remains VND and appointment times remain in the salon's Vietnam time zone.

Catalog and booking documents declare `lang="en"`. Shared navigation and account links use English on those pages through `EnglishInterface`; other application pages retain their existing language.

Stored service names, descriptions, group names, stylist names and specialties are business content and are not rewritten. Vietnamese accent-insensitive search remains supported and tested. Existing Vietnamese demo records also remain unchanged.

This branch builds on the auto-assignment work in PR #38. Merge that PR first when reviewing only the English interface changes.

Validation: 151 .NET tests passed; real MVC/Razor browser checks passed for booking, stylist assignment, search and clearing search. Catalog at 360px with 100 services/10 groups had no horizontal overflow or clipped text. Five cold-browser-cache 4G simulations completed in 0.584–0.854 seconds (localhost/InMemory, not production acceptance).
