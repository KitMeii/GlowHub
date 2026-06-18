# Tóm tắt nghiệp vụ Admin — GlowHub (ngắn gọn)

**Mục tiêu**: ghi lại quy tắc nghiệp vụ quan trọng cho phần Admin (quyền, endpoints, validation, tác động DB).

- **Quyền truy cập**: tất cả endpoint Admin yêu cầu role `Admin` (attribute `[Authorize(Roles = "Admin")]`). Ví dụ: [BaseCore/BaseCore.APIService/Controllers/AdminStatsController.cs](BaseCore/BaseCore.APIService/Controllers/AdminStatsController.cs), [BaseCore/BaseCore.AuthService/Controllers/UserController.cs](BaseCore/BaseCore.AuthService/Controllers/UserController.cs), [BaseCore/BaseCore.APIService/Controllers/AuditLogController.cs](BaseCore/BaseCore.APIService/Controllers/AuditLogController.cs).

**Các nhóm nghiệp vụ chính**

- **Thống kê (Overview)**
  - Endpoint: `GET /api/admin/stats/overview`
  - Tính toán: doanh thu theo ngày (loại trừ `Cancelled`), đếm theo trạng thái, top shops theo `SubOrder` (revenue, commission, net), totals (orders, customers).
  - Triển khai: aggregation ở DB (GroupBy/Sum/Count) → trả về các ngày thiếu bằng 0.
  - File: [BaseCore/BaseCore.APIService/Controllers/AdminStatsController.cs](BaseCore/BaseCore.APIService/Controllers/AdminStatsController.cs)

- **Người dùng (Admin)**
  - Endpoints admin: `GET /api/users` (paging/search), `POST /api/users` (create), `PUT /api/users/{id}`, `DELETE /api/users/{id}`.
  - Lưu ý: user có thể tự xem/cập nhật profile qua `GET/PUT /api/users/profile` nhưng không được đổi `UserType` hoặc `IsActive`.
  - File: [BaseCore/BaseCore.AuthService/Controllers/UserController.cs](BaseCore/BaseCore.AuthService/Controllers/UserController.cs)

- **Roles & Permissions**
  - Roles tĩnh (Admin, User, Seller) và mapping permission mẫu (permissions strings). File: [BaseCore/BaseCore.AuthService/Controllers/RolesController.cs](BaseCore/BaseCore.AuthService/Controllers/RolesController.cs)

- **Đơn hàng (Admin)**
  - Endpoints admin chính: `GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `PUT /api/admin/orders/{id}/status`.
  - Valid statuses admin có thể set: Pending, Confirmed, Shipping, Delivered, Completed, Cancelled (controller kiểm tra hợp lệ).
  - Khi cập nhật status → ghi `OrderStatusHistory` (ChangedBy = userId). Nếu chuyển sang `Cancelled` từ trạng thái khác (không phải `Completed`), hệ thống sẽ RESTOCK: tăng `Product.Stock` theo `OrderDetails`.
  - Checkout (khách) là quá trình phức tạp: multi-shop SubOrders, dynamic shipping, hệ thống voucher, shop voucher, freeship; atomic stock decrement dùng raw SQL UPDATE (WHERE Stock >= qty) để tránh race condition; audit log cho event tài chính `ORDER_CREATE`.
  - File: [BaseCore/BaseCore.APIService/Controllers/OrdersController.cs](BaseCore/BaseCore.APIService/Controllers/OrdersController.cs)

- **Vouchers (Admin & User flows)**
  - Public endpoints: `GET /api/vouchers/public`, `GET /api/vouchers/my`, `POST /api/vouchers/save/{code}`, `DELETE /api/vouchers/unsave/{code}`, `POST /api/vouchers/validate`, `GET /api/vouchers/available`.
  - Admin CRUD: `GET /api/vouchers` (paging), `POST /api/vouchers` (create), `PUT /api/vouchers/{id}`, `DELETE /api/vouchers/{id}`, `GET /api/vouchers/{id}/usage`.
  - Validation rules: code stored uppercase; check `IsActive`, `StartDate`/`ExpiryDate`, `UsageLimit` vs `UsedCount`, `MinOrderAmount`; discount calc: percent or fixed, apply `MaxDiscount` cap.
  - Admin actions update `UsedCount`, `CreatedAt` and write audit logs for create/update/delete.
  - File: [BaseCore/BaseCore.APIService/Controllers/VouchersController.cs](BaseCore/BaseCore.APIService/Controllers/VouchersController.cs)

- **Audit logs**
  - Endpoint admin: `GET /api/admin/audit-logs` (filter by action/user/from-to, paged). Audit table lưu `OldValue`/`NewValue`/`IpAddress`/`CreatedAt`.
  - File: [BaseCore/BaseCore.APIService/Controllers/AuditLogController.cs](BaseCore/BaseCore.APIService/Controllers/AuditLogController.cs)

- **Seller / SubOrder behaviour**
  - Seller endpoints (role Seller) quản lý SubOrders: confirm, ship, cancel; SubOrder là nguồn truth cho trạng thái shop → parent Order sync theo `SyncOrderStatus`.
  - Khi seller `Ship` một SubOrder, `Order.TrackingCode` có thể trở thành "Nhiều đơn vị vận chuyển" nếu nhiều SubOrders đang ship.
  - File: [BaseCore/BaseCore.APIService/Controllers/OrdersController.cs](BaseCore/BaseCore.APIService/Controllers/OrdersController.cs)

**Các ràng buộc & validation quan trọng (tóm tắt nhanh)**

- Voucher: kiểm tra `StartDate`/`ExpiryDate`, `UsageLimit`, `MinOrderAmount`, `ShopId` tương ứng (system vs shop voucher), uppercase `Code`.
- Order cancel: khách chỉ được hủy khi `Pending` (cần lý do cho một số endpoint); admin có thể set `Cancelled` (kích hoạt restock nếu applicable).
- Stock: trước khi checkout có pre-check; thực tế trừ stock bằng raw UPDATE có điều kiện `Stock >= qty`; nếu UPDATE trả về 0 rows → lỗi "hết hàng".
- Checkout: phân chia SubOrders theo shop, tính phí ship theo vùng & trọng lượng, áp voucher hệ thống trước, voucher shop riêng, freeship áp sau cùng lên tổng shipping.
- Payout: khi SubOrder/Order chuyển đến `Delivered`, `PayoutStatus` chuyển sang `WaitingRelease` (seller payout workflow).
- Audit: các sự kiện như `ORDER_CREATE`, `VOUCHER_CREATE/UPDATE/DELETE` được log; audit failures không block user response.

**Tài liệu tham chiếu (file)**

- Thống kê: [BaseCore/BaseCore.APIService/Controllers/AdminStatsController.cs](BaseCore/BaseCore.APIService/Controllers/AdminStatsController.cs)
- Users: [BaseCore/BaseCore.AuthService/Controllers/UserController.cs](BaseCore/BaseCore.AuthService/Controllers/UserController.cs)
- Roles: [BaseCore/BaseCore.AuthService/Controllers/RolesController.cs](BaseCore/BaseCore.AuthService/Controllers/RolesController.cs)
- Orders + Checkout + Seller/SubOrder: [BaseCore/BaseCore.APIService/Controllers/OrdersController.cs](BaseCore/BaseCore.APIService/Controllers/OrdersController.cs)
- Vouchers: [BaseCore/BaseCore.APIService/Controllers/VouchersController.cs](BaseCore/BaseCore.APIService/Controllers/VouchersController.cs)
- Audit logs: [BaseCore/BaseCore.APIService/Controllers/AuditLogController.cs](BaseCore/BaseCore.APIService/Controllers/AuditLogController.cs)

Nếu bạn muốn, tôi có thể:

- Bổ sung sơ đồ luồng (checkout → suborders → payout) hoặc
- Tách chi tiết validation/response shape cho từng endpoint (ví dụ body/response mẫu) hoặc
- Tạo checklist QA để kiểm tra các kịch bản admin (hủy đơn, refund, voucher edge cases).
