# Versions

## 1.4.0 — 2025-12-16
- Reimplementation of ConcurrentRpcServer to improve concurrency using NetMQPoller and NetMQQueue.

## v1.3.1 — 2025-07-30
- Minor changes regarding ConcurrentRpcServer route disposing

## v1.3.0 — 2025-06-16
- Upgrading to NetMQ v4.0.1.16 — new design based on RouterSocket + NetMQQueue. Replaced Router + BlockingCollection pattern and DealerSocket on the client side to achieve concurrency.

## v1.3.0 — 2025-02-05
- Improved log handling and trace capture.

## v1.2.1 — 2024-08-05
- License file added to the package.

## 1.2.0 — 2024-08-05
- Version fixed to 1.2.0 (note: commit indicates a version correction).
- Repository URL added.
- Miscellaneous short messages (auxiliary records).

## 1.3.0 — 2023-11-15
- Added optional timeout parameter to RpcFactory.CreateClient (method overloads).

## 1.2.0 — 2023-10-30
- Improved server stop (unbinding address to avoid problems when starting another server on the same address immediately after stopping the previous one), improved debug of serialized messages (hide bytes in JSON output for byte[]), SimpleDemo includes a test for sending files.
- Auxiliary messages.

## v1.1 / v1.1 fix — 2023-10-20
- Reimplementation of MethodMatcher.Match to search in additional interfaces of a type when no matches are found at the root level.

## v1.0 — 2023-10-19
- Initial release.