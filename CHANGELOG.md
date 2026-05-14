# Changelog

## [1.1.0](https://github.com/applicapr/ChangeOrder/compare/changeorder-api-v1.0.0...changeorder-api-v1.1.0) (2026-05-14)


### Features

* **bootstrap:** add onion solution skeleton (T001-T013) ([ab1cc1e](https://github.com/applicapr/ChangeOrder/commit/ab1cc1e2bfdb427b69b8da9087d3e2bf1c93f657))
* change order management feature (001) — SDD complete, US1+US2+US3+Polish ([1dd5d67](https://github.com/applicapr/ChangeOrder/commit/1dd5d675e352e9dbd358d20179ae3caf913c2702))
* change order management feature (001) — SDD complete, US1+US2+US3+Polish ([1dd5d67](https://github.com/applicapr/ChangeOrder/commit/1dd5d675e352e9dbd358d20179ae3caf913c2702))
* **foundational:** add onion plumbing, audit interceptor and initial migration (T014-T049) ([f2e0175](https://github.com/applicapr/ChangeOrder/commit/f2e0175080bfa160c62b0eafd24142dbb8a6946f))
* **host:** add scalar interactive api docs and enrich endpoint metadata ([aca3708](https://github.com/applicapr/ChangeOrder/commit/aca3708e8e5911400698e18b182a1763b79a580d))
* **infra:** add docker-compose stack with sql server and migrations sidecar ([4178cdb](https://github.com/applicapr/ChangeOrder/commit/4178cdb66f313221a14fa7d038eacc07a46069c7))
* **plan:** add implementation plan, research, data model, quickstart and OpenAPI contract ([7b0650c](https://github.com/applicapr/ChangeOrder/commit/7b0650c0e42cdd97743f73b74a9957258c519573))
* **polish:** finalize cross-cutting concerns (T086-T094) ([1ed49e3](https://github.com/applicapr/ChangeOrder/commit/1ed49e3dad0fa7ea479ed7e24d0e562d2c130473))
* **presentation:** add GET /version operational endpoint (T088a) ([773b68c](https://github.com/applicapr/ChangeOrder/commit/773b68c9345028cee9e2858688141d4855b7205b))
* **query:** add ?orderNumber= prefix filter to listing endpoint (T088b) ([4e9d777](https://github.com/applicapr/ChangeOrder/commit/4e9d777af91616a29553cad5ae6abe619e8e9786))
* **speckit:** adopta GitHub Spec Kit con constitución v1.0.0 ([2f7877e](https://github.com/applicapr/ChangeOrder/commit/2f7877e66dc83f20a598cfe03f72f48a3c044367))
* update README with project details ([8fd69c4](https://github.com/applicapr/ChangeOrder/commit/8fd69c49d90b0c84935d73e2813455726326c838))
* **us1:** create change order endpoint with idempotency (T050-T062) ([1003bb0](https://github.com/applicapr/ChangeOrder/commit/1003bb04aa8383c502a04a970093afa2c76ffb23))
* **us2:** approval workflow and milestone dates (T063-T071) ([dba0730](https://github.com/applicapr/ChangeOrder/commit/dba0730e7263492584bef47c901fc590dd25d28f))
* **us3:** list, get-by-id, update, soft-delete change orders (T072-T085) ([b75d3c6](https://github.com/applicapr/ChangeOrder/commit/b75d3c6f32a9b4b18790d6422e4deb25bc36d54a))


### Bug Fixes

* **data,business:** map SQL 1205 to retryable DeadlockVictim (R-1) ([e56d9af](https://github.com/applicapr/ChangeOrder/commit/e56d9af71038108074b0143346676d61f8dca4ae))
* **data:** soft-delete preserves owned entity values on UPDATE ([94c317b](https://github.com/applicapr/ChangeOrder/commit/94c317bb22f3be862fc6759d1d6794b3914e4964))
* **host:** silence spurious HostAbortedException fatal log from ef tooling ([de0b096](https://github.com/applicapr/ChangeOrder/commit/de0b0965140d650d1f87c492495f872687303fa1))


### Performance

* **data,business:** optimize read paths and align CreateOrder with R-1 tx scope ([6ec2016](https://github.com/applicapr/ChangeOrder/commit/6ec2016f955f4f06415d9fe4699451d4bb6941b5))
