# GlowHub — Đặc Tả Hoàn Chỉnh v2
> Cập nhật: 2026-06-11 | Bao gồm mọi thay đổi đến commit `bc09bdc`

---

# PHẦN 1: LUỒNG NGHIỆP VỤ

## 1.1 Customer — Từng Trang, Từng Tính Năng

### Trang Chủ (`index.html`)
- **Hero Banner**: Ảnh nền + tiêu đề + CTA lấy từ `SiteSettings` (group=homepage). Fallback về Unsplash hardcode nếu DB trống. Admin đổi được qua `PUT /api/SiteSettings/{key}`.
- **Flash Sale**: Ẩn theo mặc định (`display:none`). Hiện + đếm ngược khi có Flash Sale đang chạy (`GET /api/flashsale/active`). Tự động ẩn nếu hết hàng hoặc hết giờ.
- **Split Panels (Banner giữa)**: 2 banner lấy từ `GET /api/Banners` (lọc IsActive=true, sort theo SortOrder). Fallback ảnh Unsplash khi DB trống.
- **Hàng Mới Về (Asymmetric Grid)**: Ưu tiên theo thứ tự: (1) `GET /api/products?isNew=true&limit=5`, (2) `GET /api/FeaturedProducts?section=new_arrivals`, (3) demo fallback 6 SP hardcode.
- **Danh Mục Nổi Bật**: Lấy từ `GET /api/categories` (3 danh mục đầu tiên). Ảnh từ `Category.ImageUrl`; nếu null thì dùng `localImgs[index]` Unsplash fallback.
- **Bán Chạy Nhất**: `GET /api/products?limit=8&sort=bestseller` → sort theo `Product.SoldCount` giảm dần.
- **Xem Gần Đây**: Nếu đăng nhập → `GET /api/recently-viewed` (Bearer). Nếu chưa đăng nhập → đọc `localStorage["gh_recently_viewed"]`. Hiện tối đa 10 SP.
- **Navbar**: Categories dropdown tải từ `GET /api/categories`. Cart badge, wishlist badge cập nhật real-time từ localStorage/API.

### Cửa Hàng (`shop.html`)
- **Bộ lọc**: Category (dropdown), giá (min/max), đánh giá (≥ N sao), từ khóa, sort (mới nhất/giá tăng/giá giảm/bán chạy nhất/tên A-Z).
- **API call**: `GET /api/products?keyword=&categoryId=&minPrice=&maxPrice=&sort=&page=&pageSize=20`
- **Pagination**: `{ items[], totalCount, page, pageSize, totalPages }`. Frontend render "Trang X / Y", nút Prev/Next.
- **Xem gần đây**: Strip nhỏ cuối trang từ localStorage / API.
- **Search**: Lịch sử tìm kiếm lưu localStorage. Gợi ý từ kết quả API.

### Chi Tiết Sản Phẩm (`product.html`)
- Load: `GET /api/products/{id}` → hiển thị tên, mô tả, giá, gallery ảnh, thông số kỹ thuật JSON.
- **Gallery**: Ảnh chính + thumbnail row. Click thumbnail → đổi ảnh chính. Zoom on hover.
- **Thông tin Shop mini**: `GET /api/shops/{shopId}/profile` (tên, logo, rating, số SP).
- **Kiểm tra đã mua**: `GET /api/orders/my/check-purchased/{productId}` → nếu đã mua thì unlock form review.
- **Đánh giá**: `GET /api/products/{productId}/reviews?page=1&limit=10`. Gửi review: `POST /api/products/{productId}/reviews` (kèm ảnh base64 Cloudinary).
- **Q&A**: `GET /api/qna/product/{productId}`. Đặt câu hỏi: `POST /api/qna/ask` (Bearer).
- **Sản phẩm liên quan**: `GET /api/products?categoryId=X&limit=4`.
- **Thêm giỏ**: Gọi `Cart.add()` → nếu đăng nhập `POST /api/cart/add`; nếu không → localStorage `gh_cart`.
- **Wishlist**: Toggle `POST/DELETE /api/wishlist/{productId}`. Đồng bộ localStorage `gh_wishlist`.
- **So sánh**: Thêm vào `gh_compare` (max 3). Nút "So Sánh" chuyển sang `compare.html`.
- **Lịch sử xem**: `POST /api/recently-viewed/{productId}` (Bearer) + thêm vào localStorage.
- **Sticky bar**: Xuất hiện khi scroll qua phần giá, hiện nút "Thêm giỏ" nhanh.
- **Share**: Facebook, Zalo, copy link.

### Trang Shop Public (`shop-profile.html`)
- `GET /api/shops/{shopId}/profile`: Banner, logo, tên, mô tả, địa chỉ, ngày tạo.
- `GET /api/shops/{shopId}/stats`: Doanh thu ẩn, tổng SP, tổng đơn, rating trung bình.
- `GET /api/shops/{shopId}/products?sort=&page=&limit=`: Sản phẩm của shop.
- `GET /api/shops/{shopId}/reviews`: Reviews tổng hợp của shop.
- **Theo dõi**: `POST /api/shops/{id}/follow` / `DELETE /api/shops/{id}/follow`.

### Giỏ Hàng (`cart.html`)
- **Load**: Đọc `gh_cart` localStorage → với từng SP, fetch thông tin shop (`/api/shops/{shopId}/profile`).
- **Cập nhật số lượng**: Debounce 400ms. Gọi `PUT /api/cart/update` (Bearer) hoặc cập nhật localStorage.
- **Xóa item**: `DELETE /api/cart/{cartItemId}` hoặc xóa khỏi localStorage.
- **Progress bar**: Tổng đơn vs ngưỡng miễn phí ship 500.000đ.
- **Voucher**: Nhập mã → `POST /api/vouchers/validate` → hiển thị số tiền giảm. Lưu voucher đã áp vào `gh_applied_voucher`.
- **Tính phí ship**: `POST /api/shipping/calculate-cart` (từ địa chỉ + giỏ hàng).
- **Checkout**: Lưu `glowhub_checkout` vào localStorage → redirect `checkout.html`.

### Thanh Toán (`checkout.html`)
- **Bước 1 — Địa chỉ**: `GET /api/addresses` (các địa chỉ đã lưu). Thêm mới: `POST /api/addresses`. Đặt mặc định: `PUT /api/addresses/{id}/set-default`.
- **Bước 2 — Xem lại đơn**: Đọc `glowhub_checkout` từ localStorage. Hiển thị SP, tổng, voucher, phí ship.
- **Bước 3 — Thanh toán**:
  - COD: `POST /api/orders/checkout` → tạo đơn ngay, redirect `order-success.html`.
  - VNPay: `POST /api/payment/vnpay/create` → nhận `paymentUrl` → redirect sang VNPay. Sau khi thanh toán VNPay redirect về `GET /api/payment/vnpay/return`.
  - Chuyển khoản ngân hàng: `POST /api/orders/checkout` (paymentMethod=BankTransfer) → redirect `waiting-payment.html`. Xem QR: `GET /api/payment/bank/info?orderId=`. Xác nhận: `POST /api/payment/bank/submit`.
- **Sau checkout thành công**: Xóa `glowhub_checkout`, xóa `gh_cart`, hiện mã đơn `ORD-XXXXXX`.

### Theo Dõi Đơn Hàng (`order-tracking.html`)
- Tra cứu không cần đăng nhập: `GET /api/orders/{orderCode}/track` (bằng mã + SĐT).
- Đăng nhập: `GET /api/orders/my/{orderId}/track` → timeline 4 bước.
- Hủy đơn (khi Pending): `POST /api/orders/my/{orderId}/cancel`.
- Xác nhận đã nhận (khi Shipping): `POST /api/orders/my/{orderId}/received`.

### Hồ Sơ Cá Nhân (`profile.html`)
- **Tab Tổng Quan**: Thông tin user, avatar, cập nhật `PUT /api/auth/profile`.
- **Tab Đơn Hàng**: `GET /api/orders/my?page=1&limit=10` → timeline, chi tiết từng đơn.
- **Tab Wishlist**: `GET /api/wishlist` → grid sản phẩm yêu thích. Xóa: `DELETE /api/wishlist/{productId}`.
- **Tab Ví GlowHub**: `GET /api/customer/wallet` → số dư, lịch sử giao dịch. Nạp tiền: `POST /api/customer/wallet/topup`.
- **Tab Địa Chỉ**: CRUD địa chỉ qua `/api/addresses`.
- **Tab Khiếu Nại**: `GET /api/disputes/my` → danh sách. Gửi mới: `POST /api/disputes`.

### Voucher (`vouchers.html`)
- `GET /api/vouchers/public` → tất cả voucher công khai còn hiệu lực.
- `GET /api/vouchers/my` → voucher đã lưu.
- Lưu: `POST /api/vouchers/save/{code}`. Bỏ lưu: `DELETE /api/vouchers/unsave/{code}`.
- Copy code bằng 1 click.

### Flash Sale (`flash-sale.html`)
- `GET /api/flashsale/active` → Flash Sale đang chạy + danh sách SP.
- `GET /api/flashsale/upcoming` → Flash Sale sắp diễn ra (24h tới).
- Đếm ngược realtime. Progress bar số lượng đã bán.
- Mua Flash Sale: `POST /api/flashsale/buy` (atomic, chống race condition bằng SQL UPDATE ... WHERE RemainingQuantity >= qty).

### So Sánh (`compare.html`)
- Đọc `gh_compare` localStorage (max 3 SP ID).
- `POST /api/compare/add` + `GET /api/compare` → lấy thông tin chi tiết 3 SP.
- Highlight ô tốt nhất (giá thấp nhất, rating cao nhất).
- Thêm vào giỏ trực tiếp từ trang so sánh.

### Đăng Nhập / Đăng Ký (`login.html`, `register.html`)
- Login: `POST /api/Auth/login` → JWT token + user info → lưu `token`, `user` vào localStorage.
- Register: `POST /api/Auth/register`.
- OAuth Google/Facebook: redirect → `/api/oauth/google` hoặc `/api/oauth/facebook`.
- Remember me: lưu `gh_remember` localStorage.
- Sau login: đọc `gh_redirect` localStorage → redirect về trang đã lưu.

---

## 1.2 Seller — Từng Tab, Từng Tính Năng

### Đăng Ký Shop (`register-shop.html`)
- `POST /api/shops/register` (Seller role) → tạo shop trạng thái Pending.
- Nhận thông báo khi Admin duyệt hoặc từ chối.

### Dashboard Tổng Quan (Tab mặc định)
- `GET /api/shops/my/dashboard`: 4 thẻ (doanh thu hôm nay, tháng này, tổng đơn, đơn chờ).
- Biểu đồ doanh thu 7 ngày.
- Danh sách SP sắp hết hàng (`GET /api/inventory/low-stock`).

### Tab Sản Phẩm
- Danh sách: `GET /api/products` (filter by shopId từ JWT).
- Thêm: `POST /api/products` với upload ảnh Cloudinary trước (`POST /api/upload/image`).
- Sửa: `PUT /api/products/{id}`.
- Xóa/Ẩn: toggle `IsActive` qua `PUT /api/products/{id}`.
- Upload gallery nhiều ảnh: `POST /api/upload/image` lần lượt → lưu URLs vào `Product.ImageGallery` (JSON array).
- Tìm kiếm + lọc theo trạng thái trực tiếp trên bảng.

### Tab Đơn Hàng
- `GET /api/orders/shop?status=&page=1&limit=20`: Đơn hàng của shop.
- Xác nhận: `PUT /api/orders/shop/{orderId}/confirm` (Pending → Confirmed).
- Giao hàng: `PUT /api/orders/shop/{orderId}/ship` (Confirmed → Shipping).
- Hủy: `PUT /api/orders/shop/{orderId}/cancel` kèm lý do.
- Modal chi tiết đơn: thông tin khách, địa chỉ, timeline, các sản phẩm.

### Tab Tồn Kho
- `GET /api/inventory/my`: Danh sách tồn kho toàn shop.
- Cập nhật số lượng: `PUT /api/inventory/{productId}`.
- Alert khi `Stock < 10`.

### Tab Voucher Shop
- `GET /api/seller/vouchers`: Danh sách voucher đã tạo.
- Tạo: `POST /api/seller/vouchers` (% hoặc số tiền cố định, điều kiện đơn tối thiểu, giới hạn lần dùng, thời gian).
- Sửa: `PUT /api/seller/vouchers/{id}`. Xóa: `DELETE /api/seller/vouchers/{id}`.

### Tab Đánh Giá
- `GET /api/reviews/shop`: Reviews của shop, lọc theo SP.
- Phản hồi: `POST /api/reviews/{reviewId}/reply` (chỉ 1 lần/review).
- Stats: `GET /api/reviews/shop/stats` (tỷ lệ phản hồi, rating trung bình).

### Tab Q&A
- `GET /api/qna/shop`: Câu hỏi chưa trả lời.
- Trả lời: `POST /api/qna/{questionId}/answer`.
- Stats: `GET /api/qna/shop/stats`.

### Tab Thống Kê
- `GET /api/reports/seller/revenue?from=&to=`: Doanh thu theo ngày (line chart).
- `GET /api/reports/seller/products?from=&to=`: Top SP bán chạy (bar chart, pie chart).
- `GET /api/reports/seller/summary`: Tổng hợp doanh thu thô, hoa hồng, thực nhận.

### Tab Thông Báo
- `GET /api/notifications/my?page=1&limit=20`.
- Đánh dấu đọc: `PUT /api/notifications/{id}/read` / `PUT /api/notifications/read-all`.
- Unread count badge: `GET /api/notifications/unread-count`.
- Nhận thông báo khi: đơn mới, SP sắp hết, review mới, câu hỏi mới, đơn bị hủy.

### Tab Ví Seller
- `GET /api/seller/wallet`: Số dư, tổng doanh thu, tổng hoa hồng đã trả.
- `GET /api/seller/wallet/transactions?type=&page=&limit=`: Lịch sử giao dịch.

### Tab Thông Tin Shop
- `GET /api/shops/my`: Thông tin hiện tại.
- Cập nhật: `PUT /api/shops/{id}` (tên, mô tả, địa chỉ, SĐT, logo).
- Hiển thị: trạng thái duyệt, ngày tạo, tỷ lệ hoa hồng, số tiền thực nhận.

---

## 1.3 Admin — 16 Tab, Từng Tính Năng

### Tab Dashboard
- Stats cards (6): tổng users, tổng orders, doanh thu hôm nay, doanh thu tháng, tổng shops, shops chờ duyệt.
- Biểu đồ doanh thu 30 ngày.
- Top shop bán chạy, top SP bán chạy.
- 10 đơn hàng mới nhất.

### Tab Người Dùng
- `GET /api/admin/users?page=&search=&role=&status=`: Danh sách, tìm kiếm, lọc.
- Ban: `PUT /api/admin/users/{id}/ban`. Unban: `PUT /api/admin/users/{id}/unban`.
- Đổi role: `PUT /api/admin/users/{id}/role` (không đổi role chính mình).
- Xóa: `DELETE /api/admin/users/{id}`.

### Tab Cửa Hàng
- `GET /api/shops/admin/all`: Danh sách tất cả shop (kể cả Pending).
- Duyệt: `PUT /api/shops/admin/{id}/approve`. Khóa: `PUT /api/shops/admin/{id}/ban`.
- Xem stats từng shop: `GET /api/shops/admin/{id}/stats`.
- Điều chỉnh hoa hồng riêng: `PUT /api/shops/admin/{id}/commission`.

### Tab Sản Phẩm
- Xem toàn bộ SP trên sàn, lọc theo shop + danh mục.
- Ẩn/hiện SP vi phạm bằng toggle IsActive.

### Tab Danh Mục
- `GET /api/categories`: Danh sách.
- Tạo: `POST /api/categories` (Tên, Mô tả, ImageUrl).
- Sửa: `PUT /api/categories/{id}` (bao gồm cả ImageUrl). Xóa (soft): `DELETE /api/categories/{id}` — không xóa được nếu còn SP.

### Tab Đơn Hàng
- `GET /api/orders/admin/orders?status=&shopId=&from=&to=&page=&limit=`: Tất cả đơn.
- Xem chi tiết: `GET /api/orders/admin/orders/{id}`.
- Force-change trạng thái: `PUT /api/orders/admin/orders/{id}/status` kèm ghi chú.

### Tab Banner
- `GET /api/Banners/all`: Tất cả banner kể cả inactive.
- Tạo: `POST /api/Banners` (Title, Subtitle, ImageUrl, LinkUrl, ButtonText, SortOrder, IsActive).
- Sửa: `PUT /api/Banners/{id}`. Xóa: `DELETE /api/Banners/{id}`.
- Sắp xếp thứ tự: `PUT /api/Banners/reorder`.

### Tab Flash Sale
- `GET /api/flashsale/admin/list?page=&limit=`: Danh sách tất cả (active/upcoming/ended).
- Tạo: `POST /api/flashsale` (Tên, StartTime, EndTime, danh sách SP + giá sale + số lượng).
- Sửa: `PUT /api/flashsale/{id}`. Xóa: `DELETE /api/flashsale/{id}`.
- Bật/tắt: `PUT /api/flashsale/{id}/toggle`.
- Thêm SP vào sale: `POST /api/flashsale/{id}/products`. Xóa SP: `DELETE /api/flashsale/{id}/products/{productId}`.

### Tab Voucher (Toàn Sàn)
- `GET /api/vouchers`: Tất cả voucher.
- Tạo: `POST /api/vouchers`. Sửa: `PUT /api/vouchers/{id}`. Xóa: `DELETE /api/vouchers/{id}`.
- Xem lịch sử dùng: `GET /api/vouchers/{id}/usage`.

### Tab Hoa Hồng & Giải Ngân
- `GET /api/admin/commission/summary`: Tổng quan hoa hồng.
- `GET /api/admin/commission/shops`: Hoa hồng từng shop.
- Giải ngân: `POST /api/admin/commission/payout/{shopId}`.
- Lịch sử giải ngân: `GET /api/admin/commission/history`.
- Ví Seller: `GET /api/admin/wallet/overview`. Giải ngân theo SubOrder: `POST /api/admin/wallet/release-payouts`.

### Tab Khiếu Nại
- `GET /api/disputes`: Tất cả khiếu nại.
- Chi tiết: `GET /api/disputes/{id}` (mô tả, ảnh bằng chứng).
- Đang xử lý: `PUT /api/disputes/{id}/process`.
- Phán quyết: `PUT /api/disputes/{id}/resolve` (favor=customer → hoàn tiền, trừ doanh thu Seller | favor=shop → bác bỏ).

### Tab Thanh Toán Ngân Hàng
- `GET /api/payment/admin/bank/pending`: Đơn chờ xác nhận chuyển khoản.
- Xác nhận nhận tiền: `POST /api/payment/admin/bank/confirm/{orderId}`.

### Tab Audit Log
- `GET /api/admin/audit-logs?page=&limit=&action=`: Lịch sử hành động Admin.
- Ghi nhận: duyệt/ban shop, khóa user, đổi hoa hồng, tạo/xóa Flash Sale, thay đổi quyền.
- Mỗi bản ghi có: `userId`, `action`, `entity`, `oldValue`, `newValue`, `timestamp`.

### Tab Báo Cáo & Export
- `GET /api/admin/export/revenue?from=&to=` → CSV.
- `GET /api/admin/export/orders?from=&to=` → CSV.
- `GET /api/admin/export/users` → CSV.
- `GET /api/admin/export/shops` → CSV.

### Tab Ví Khách Hàng
- `GET /api/admin/customer-wallet/overview`: Tổng quan ví tất cả khách.
- Điều chỉnh số dư: `POST /api/admin/customer-wallet/adjust`.

### Tab Cài Đặt Hệ Thống
- `GET /api/SiteSettings?group=homepage`: Settings trang chủ.
- `GET /api/SiteSettings/detail?group=`: Chi tiết đầy đủ (admin only).
- Cập nhật: `PUT /api/SiteSettings/{key}` hoặc `PUT /api/SiteSettings/bulk`.
- Keys homepage: `hero_img`, `hero_title`, `hero_sub`, `hero_eyebrow`, `hero_cta`.
- SystemConfig: `GET /api/config`, `PUT /api/config/{key}`.

---

## 1.4 Luồng Liên Kết 3 Bên

### Luồng Đặt Hàng → Giải Ngân
```
Customer checkout → POST /api/orders/checkout
  Backend Transaction:
    1. Re-tính giá từ DB (không tin frontend)
    2. Kiểm tra Stock từng SP
    3. Trừ Stock + tăng SoldCount
    4. Tạo Order (status=Pending, paymentStatus=UNPAID)
    5. Tạo OrderDetails
    6. Tính CommissionRate (từ Shop.CommissionRate, default 10%)
    7. Tính SellerPayoutAmount = TotalAmount - CommissionAmount
    8. Xóa CartItems
    9. Gửi Notification cho Seller
  Commit Transaction

Seller: Confirm → ship → Customer: Received
  POST /api/orders/my/{id}/received
    → Order.Status = Completed
    → SubOrder.PayoutStatus = WAITING_RELEASE

Admin: Giải ngân
  POST /api/admin/wallet/release-payouts
    → Duyệt SubOrders WAITING_RELEASE → RELEASED
    → Cộng tiền vào SellerWallet
```

### Luồng Voucher: Tạo → Lưu → Áp Dụng
```
Admin/Seller tạo voucher (POST /api/vouchers hoặc /api/seller/vouchers)
  → Voucher lưu DB với: Code, DiscountType, DiscountValue, MinOrderValue,
    MaxUsage, UsedCount, StartDate, EndDate, IsActive

Customer:
  1. Xem: GET /api/vouchers/public
  2. Lưu: POST /api/vouchers/save/{code} → UserVoucher record
  3. Khi checkout: POST /api/vouchers/validate { code, orderAmount }
     → trả về: discountAmount, finalAmount
  4. Áp dụng: lưu vào gh_applied_voucher localStorage
  5. Khi tạo đơn: backend validate lại voucher từ DB (không tin frontend)
```

### Trạng Thái Đơn: 3 Chiều Đồng Bộ
```
Customer view     Seller view       Admin view
─────────────     ───────────       ──────────
Đặt hàng      →  Đơn mới (Pending) → Chờ xác nhận
Chờ xác nhận  ←  Xác nhận          → Confirmed
Đang giao     ←  Đánh dấu giao     → Shipping
Đã nhận       →  (auto Completed)  → Completed
Hủy đơn       →  Đơn bị hủy        → Cancelled
                                    ↳ Restock: Stock += qty

Các trạng thái: Pending | Confirmed | Shipping | Completed | Cancelled | Returned
```

### Luồng Đánh Giá
```
Customer nhận hàng (Completed)
  → Unlock form review (check-purchased API)
  → POST /api/products/{productId}/reviews
    { rating, comment, images[] }
    → Upload ảnh: POST /api/upload/image (Cloudinary)
      cloud=dcucbyzdo, preset=GlowHub_Upload
    → Lưu Review vào DB

Seller nhận Notification "Review mới"
  → Xem: GET /api/reviews/shop
  → Phản hồi: POST /api/reviews/{reviewId}/reply (chỉ 1 lần)

Admin có thể xóa review vi phạm: DELETE /api/admin/reviews/{id}
```

### Luồng Upload Ảnh (Cloudinary)
```
POST /api/upload/image (multipart/form-data, Bearer)
  → Backend forward lên Cloudinary
    URL: https://api.cloudinary.com/v1_1/dcucbyzdo/image/upload
    upload_preset: GlowHub_Upload
  → Trả về { url, publicId }
  → Frontend lưu URL vào trường tương ứng (ImageUrl, ImageGallery JSON)
```

### Luồng Trang Chủ: Admin Cài Đặt → Customer Thấy
```
Admin → PUT /api/SiteSettings/bulk
  { hero_img: "url", hero_title: "...", ... }
  → Lưu vào DB bảng SiteSettings

Customer load index.html
  → JS: SiteSettings.getAll("homepage")
    → GET /api/SiteSettings?group=homepage
    → Trả về dict { hero_img, hero_title, hero_sub, ... }
  → Override HTML elements ngay lập tức
  → Nếu API lỗi/trống → hiện hardcode HTML mặc định (không crash)

Admin đổi Banner:
  PUT /api/Banners/{id} → Customer thấy ngay lần load tiếp theo
  (không cần cache invalidation vì không có cache layer)
```

---

# PHẦN 2: KỸ THUẬT

## 2.1 API Endpoints Đầy Đủ

> Tất cả endpoints qua port 5001 (APIService). Auth: 5002 (AuthService).

### Authentication (AuthService :5002)
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| POST | `/api/Auth/login` | Public | Đăng nhập → JWT |
| POST | `/api/Auth/register` | Public | Đăng ký tài khoản |
| PUT | `/api/auth/profile` | Bearer | Cập nhật hồ sơ |
| PUT | `/api/auth/change-password` | Bearer | Đổi mật khẩu |

### Products
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/products?keyword=&categoryId=&page=&pageSize=&minPrice=&maxPrice=&onlyNew=&onlySale=&sort=&isActive=` | Public | Tìm kiếm + lọc |
| GET | `/api/products/{id}` | Public | Chi tiết SP |
| POST | `/api/products` | Seller | Tạo SP mới |
| PUT | `/api/products/{id}` | Seller | Cập nhật SP |
| DELETE | `/api/products/{id}` | Seller | Xóa SP |
| GET | `/api/products/{id}/stats` | Public | soldCount, viewCount, reviewCount, avgRating |

> Sort values: `price_asc` | `price_desc` | `name` | `bestseller` | (default: newest by Id)

### Categories
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/categories` | Public | Danh sách (kèm ProductCount, ImageUrl) |
| GET | `/api/categories/{id}` | Public | Chi tiết 1 danh mục |
| POST | `/api/categories` | Admin | Tạo (Name, Description, ImageUrl) |
| PUT | `/api/categories/{id}` | Admin | Sửa (kể cả ImageUrl) |
| DELETE | `/api/categories/{id}` | Admin | Soft delete |

### Cart
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/cart` | Bearer | Xem giỏ hàng |
| GET | `/api/cart/grouped` | Bearer | Giỏ nhóm theo shop |
| POST | `/api/cart/add` | Bearer | Thêm SP `{ productId, quantity }` |
| PUT | `/api/cart/update` | Bearer | Cập nhật số lượng |
| DELETE | `/api/cart/{cartItemId}` | Bearer | Xóa 1 item |
| DELETE | `/api/cart/clear` | Bearer | Xóa toàn bộ |

### Orders
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| POST | `/api/orders/checkout` | Bearer | Tạo đơn + trừ kho (Transaction) |
| GET | `/api/orders/my?page=&limit=&status=` | Bearer | Đơn của Customer |
| GET | `/api/orders/my/{orderId}` | Bearer | Chi tiết đơn |
| GET | `/api/orders/my/{orderId}/track` | Bearer | Timeline đơn |
| POST | `/api/orders/my/{orderId}/cancel` | Bearer | Hủy đơn |
| POST | `/api/orders/my/{orderId}/received` | Bearer | Xác nhận đã nhận |
| GET | `/api/orders/my/check-purchased/{productId}` | Bearer | Đã mua chưa |
| GET | `/api/orders/shop?status=&page=&limit=` | Seller | Đơn của shop |
| PUT | `/api/orders/shop/{orderId}/confirm` | Seller | Xác nhận đơn |
| PUT | `/api/orders/shop/{orderId}/ship` | Seller | Giao hàng |
| PUT | `/api/orders/shop/{orderId}/cancel` | Seller | Hủy đơn |
| GET | `/api/orders/admin/orders?status=&shopId=&from=&to=&page=&limit=` | Admin | Tất cả đơn |
| GET | `/api/orders/admin/orders/{id}` | Admin | Chi tiết đơn (admin) |
| PUT | `/api/orders/admin/orders/{id}/status` | Admin | Force-change status |

### Vouchers
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/vouchers/public` | Public | Voucher toàn sàn còn hiệu lực |
| GET | `/api/vouchers/my` | Bearer | Voucher đã lưu |
| POST | `/api/vouchers/save/{code}` | Bearer | Lưu voucher |
| DELETE | `/api/vouchers/unsave/{code}` | Bearer | Bỏ lưu |
| POST | `/api/vouchers/validate` | Bearer | Kiểm tra + tính giảm giá |
| GET | `/api/vouchers/available?orderAmount=` | Bearer | Voucher có thể áp dụng |
| GET | `/api/vouchers` | Admin | Tất cả voucher |
| POST | `/api/vouchers` | Admin | Tạo voucher |
| PUT | `/api/vouchers/{id}` | Admin | Sửa |
| DELETE | `/api/vouchers/{id}` | Admin | Xóa |
| GET | `/api/vouchers/{id}/usage` | Admin | Lịch sử dùng |

### Seller Vouchers
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/seller/vouchers` | Seller | Voucher của shop |
| POST | `/api/seller/vouchers` | Seller | Tạo voucher shop |
| PUT | `/api/seller/vouchers/{id}` | Seller | Sửa |
| DELETE | `/api/seller/vouchers/{id}` | Seller | Xóa |

### Flash Sale
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/flashsale/active` | Public | Flash Sale đang chạy |
| GET | `/api/flashsale/upcoming` | Public | Flash Sale sắp diễn ra |
| GET | `/api/flashsale/{id}` | Public | Chi tiết 1 Flash Sale |
| POST | `/api/flashsale` | Admin | Tạo Flash Sale |
| PUT | `/api/flashsale/{id}` | Admin | Sửa |
| DELETE | `/api/flashsale/{id}` | Admin | Xóa |
| PUT | `/api/flashsale/{id}/toggle` | Admin | Bật/tắt |
| POST | `/api/flashsale/{id}/products` | Admin | Thêm SP vào sale |
| DELETE | `/api/flashsale/{id}/products/{productId}` | Admin | Xóa SP khỏi sale |
| GET | `/api/flashsale/admin/list` | Admin | Danh sách tất cả |
| POST | `/api/flashsale/buy` | Bearer | Mua Flash Sale (atomic) |

### Banners
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/Banners` | Public | Banner đang active |
| GET | `/api/Banners/all` | Admin | Tất cả banner |
| GET | `/api/Banners/{id}` | Admin | Chi tiết 1 banner |
| POST | `/api/Banners` | Admin | Tạo banner |
| PUT | `/api/Banners/{id}` | Admin | Sửa banner |
| DELETE | `/api/Banners/{id}` | Admin | Xóa banner |
| PUT | `/api/Banners/reorder` | Admin | Sắp xếp thứ tự |

### Featured Products
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/FeaturedProducts?section=new_arrivals` | Public | SP nổi bật theo section |
| POST | `/api/FeaturedProducts` | Admin | Thêm SP vào section |
| DELETE | `/api/FeaturedProducts/{id}` | Admin | Xóa khỏi section |
| PUT | `/api/FeaturedProducts/reorder` | Admin | Sắp xếp lại |

> Sections: `new_arrivals` | `best_sellers` | `hero_slider`

### Reviews & Q&A
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/products/{productId}/reviews?page=&limit=` | Public | Reviews của SP |
| POST | `/api/products/{productId}/reviews` | Bearer | Gửi review |
| PUT | `/api/products/{productId}/reviews/{reviewId}` | Bearer | Sửa review |
| DELETE | `/api/products/{productId}/reviews/{reviewId}` | Bearer | Xóa review |
| GET | `/api/reviews/shop` | Seller | Reviews trong shop |
| POST | `/api/reviews/{reviewId}/reply` | Seller | Phản hồi review |
| GET | `/api/reviews/shop/stats` | Seller | Thống kê review |
| GET | `/api/admin/reviews?page=&limit=` | Admin | Tất cả review |
| DELETE | `/api/admin/reviews/{id}` | Admin | Xóa review vi phạm |
| GET | `/api/qna/product/{productId}` | Public | Q&A của SP |
| POST | `/api/qna/ask` | Bearer | Đặt câu hỏi |
| GET | `/api/qna/shop` | Seller | Câu hỏi chưa trả lời |
| POST | `/api/qna/{questionId}/answer` | Seller | Trả lời câu hỏi |
| GET | `/api/qna/shop/stats` | Seller | Stats Q&A |

### Shops
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/shops/{id}` | Public | Profile shop |
| GET | `/api/shops/{shopId}/profile` | Public | Profile chi tiết |
| GET | `/api/shops/{shopId}/products` | Public | SP của shop |
| GET | `/api/shops/{shopId}/reviews` | Public | Reviews tổng hợp |
| GET | `/api/shops/{shopId}/stats` | Public | Thống kê shop |
| GET | `/api/shops/my` | Seller | Thông tin shop của mình |
| GET | `/api/shops/my/dashboard` | Seller | Dashboard stats |
| GET | `/api/shops/my/stats` | Seller | Thống kê chi tiết |
| POST | `/api/shops/register` | Seller | Đăng ký shop |
| PUT | `/api/shops/{id}` | Seller | Cập nhật shop |
| POST | `/api/shops/{id}/follow` | Bearer | Theo dõi shop |
| DELETE | `/api/shops/{id}/follow` | Bearer | Bỏ theo dõi |
| GET | `/api/shops/followed` | Bearer | Shop đang theo dõi |
| GET | `/api/shops/admin/all` | Admin | Tất cả shop |
| PUT | `/api/shops/admin/{id}/approve` | Admin | Duyệt shop |
| PUT | `/api/shops/admin/{id}/ban` | Admin | Khóa shop |
| GET | `/api/shops/admin/stats` | Admin | Stats toàn sàn |
| PUT | `/api/shops/admin/{id}/commission` | Admin | Đặt tỷ lệ hoa hồng |
| GET | `/api/shops/admin/{id}/stats` | Admin | Stats 1 shop |

### Wishlist
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/wishlist` | Bearer | DS sản phẩm yêu thích |
| POST | `/api/wishlist/{productId}` | Bearer | Thêm vào wishlist |
| DELETE | `/api/wishlist/{productId}` | Bearer | Xóa khỏi wishlist |
| GET | `/api/wishlist/check/{productId}` | Bearer | Kiểm tra đã thích chưa |

### Địa Chỉ
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/addresses` | Bearer | DS địa chỉ |
| POST | `/api/addresses` | Bearer | Thêm địa chỉ |
| PUT | `/api/addresses/{id}` | Bearer | Sửa địa chỉ |
| PUT | `/api/addresses/{id}/set-default` | Bearer | Đặt mặc định |
| DELETE | `/api/addresses/{id}` | Bearer | Xóa địa chỉ |

### Notifications
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/notifications/my?page=&limit=` | Bearer | Thông báo của tôi |
| GET | `/api/notifications/unread-count` | Bearer | Số chưa đọc |
| PUT | `/api/notifications/{id}/read` | Bearer | Đánh dấu đã đọc |
| PUT | `/api/notifications/read-all` | Bearer | Đọc tất cả |
| DELETE | `/api/notifications/{id}` | Bearer | Xóa 1 thông báo |

### Payment
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| POST | `/api/payment/vnpay/create` | Bearer | Tạo URL thanh toán VNPay |
| GET | `/api/payment/vnpay/return` | Public | Callback từ VNPay (redirect) |
| GET | `/api/payment/vnpay/ipn` | Public | IPN server-to-server |
| GET | `/api/payment/bank/info?orderId=` | Bearer | QR + thông tin CK ngân hàng |
| POST | `/api/payment/bank/submit` | Bearer | Xác nhận đã chuyển khoản |
| POST | `/api/payment/admin/bank/confirm/{orderId}` | Admin | Admin xác nhận nhận tiền |
| GET | `/api/payment/admin/bank/pending` | Admin | DS đơn chờ xác nhận CK |
| POST | `/api/payment/expire` | Public | Hủy đơn WAITING_PAYMENT hết hạn |

### Shipping
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/shipping/calculate?from=&to=&weight=` | Public | Tính phí ship 1 đơn |
| POST | `/api/shipping/calculate-cart` | Public | Tính phí ship toàn giỏ |
| GET | `/api/shipping/regions` | Public | DS vùng hợp lệ |

### Wallet
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/customer/wallet` | Bearer | Số dư ví customer |
| GET | `/api/customer/wallet/transactions` | Bearer | Lịch sử giao dịch |
| POST | `/api/customer/wallet/topup` | Bearer | Nạp tiền |
| GET | `/api/seller/wallet` | Seller | Số dư ví seller |
| GET | `/api/seller/wallet/transactions` | Seller | Lịch sử |
| GET | `/api/admin/wallet/overview` | Admin | Tổng quan ví seller |
| POST | `/api/admin/wallet/release-payouts` | Admin | Giải ngân SubOrders |
| GET | `/api/admin/wallet/transactions` | Admin | Lịch sử tất cả |
| GET | `/api/admin/customer-wallet/overview` | Admin | Tổng quan ví customer |
| POST | `/api/admin/customer-wallet/adjust` | Admin | Điều chỉnh số dư |

### Commission
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/admin/commission/summary` | Admin | Tổng quan hoa hồng |
| GET | `/api/admin/commission/shops` | Admin | Hoa hồng từng shop |
| POST | `/api/admin/commission/payout/{shopId}` | Admin | Giải ngân shop |
| GET | `/api/admin/commission/history` | Admin | Lịch sử giải ngân |

### Reports & Export
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/reports/seller/revenue?from=&to=` | Seller | Doanh thu theo ngày |
| GET | `/api/reports/seller/products?from=&to=` | Seller | Top SP bán chạy |
| GET | `/api/reports/seller/summary` | Seller | Tổng hợp tài chính |
| GET | `/api/admin/export/revenue` | Admin | Export CSV doanh thu |
| GET | `/api/admin/export/orders` | Admin | Export CSV đơn hàng |
| GET | `/api/admin/export/users` | Admin | Export CSV users |
| GET | `/api/admin/export/shops` | Admin | Export CSV shops |

### Misc
| Method | URL | Auth | Mô tả |
|--------|-----|------|-------|
| GET | `/api/recently-viewed` | Bearer | SP xem gần đây |
| POST | `/api/recently-viewed/{productId}` | Bearer | Thêm vào lịch sử |
| POST | `/api/compare/add` | Bearer | Thêm vào so sánh (max 3) |
| GET | `/api/compare` | Bearer | DS so sánh hiện tại |
| DELETE | `/api/compare/{productId}` | Bearer | Xóa 1 SP |
| DELETE | `/api/compare/clear` | Bearer | Xóa tất cả |
| POST | `/api/upload/image` | Bearer | Upload ảnh → Cloudinary URL |
| GET | `/api/inventory/my` | Seller | Tồn kho toàn shop |
| GET | `/api/inventory/low-stock` | Seller | SP sắp hết (< 10) |
| PUT | `/api/inventory/{productId}` | Seller | Cập nhật tồn kho |
| GET | `/api/disputes/my` | Bearer | Khiếu nại của tôi |
| GET | `/api/disputes` | Admin | Tất cả khiếu nại |
| GET | `/api/disputes/{id}` | Bearer/Admin | Chi tiết khiếu nại |
| POST | `/api/disputes` | Bearer | Gửi khiếu nại mới |
| PUT | `/api/disputes/{id}/process` | Admin | Đánh dấu đang xử lý |
| PUT | `/api/disputes/{id}/resolve` | Admin | Phán quyết cuối |
| GET | `/api/admin/audit-logs` | Admin | Audit log |
| GET | `/api/SiteSettings?group=` | Public | Lấy settings theo group |
| GET | `/api/SiteSettings/{key}` | Public | Lấy 1 setting |
| PUT | `/api/SiteSettings/{key}` | Admin | Cập nhật 1 setting |
| PUT | `/api/SiteSettings/bulk` | Admin | Cập nhật nhiều setting |
| GET | `/api/admin/users` | Admin | DS users |
| PUT | `/api/admin/users/{id}/ban` | Admin | Ban user |
| PUT | `/api/admin/users/{id}/unban` | Admin | Unban user |
| PUT | `/api/admin/users/{id}/role` | Admin | Đổi role |

---

## 2.2 Phân Trang — Chuẩn Chung Toàn Hệ Thống

### Request Format
```
GET /api/products?page=1&pageSize=20
GET /api/orders/my?page=1&limit=10
```
> Note: Products dùng `pageSize`, nhiều endpoint khác dùng `limit`. Cùng ý nghĩa.

### Response Format
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```
> Một số endpoint dùng `total` thay `totalCount`. Frontend xử lý cả hai.

### Frontend Xử Lý
```javascript
Product.getAll({ page: currentPage, pageSize: 20 })
  .then(data => {
    var items = data.items || data;  // fallback nếu API trả array trực tiếp
    renderProducts(items);
    renderPagination(data.page, data.totalPages);
  });

// Pagination UI:
// [← Trước] [1] [2] [3] ... [8] [Tiếp →]
// "Hiển thị 1-20 / 150 sản phẩm"
```

---

## 2.3 localStorage Keys và Cấu Trúc

| Key | Type | Cấu trúc / Ví dụ | Trang dùng |
|-----|------|-------------------|------------|
| `token` | string | `"eyJhbGci..."` JWT | Mọi trang |
| `user` | JSON | `{id, username, fullName, email, role, avatar}` | Mọi trang |
| `gh_cart` | JSON array | `[{id, name, price, image, quantity, shopId}]` | cart, shop, product |
| `gh_wishlist` | JSON array | `[{id, name, price, image}]` | profile, product |
| `gh_compare` | JSON array | `[id1, id2, id3]` (max 3 product IDs) | product, compare |
| `gh_recently_viewed` | JSON array | `[{id, name, price, imageUrl}]` (max 10) | index, product |
| `glowhub_checkout` | JSON | `{items[], total, voucher, shippingFee, address}` | cart → checkout |
| `gh_applied_voucher` | JSON | `{code, discountAmount, type}` | cart, checkout |
| `gh_applied_ship_voucher` | JSON | `{code, discountAmount}` | cart |
| `gh_shop_vouchers` | JSON | `{shopId: {code, discount}}` | cart |
| `gh_redirect` | string | `"profile.html#orders"` | login |
| `gh_remember` | string | `"true"` | login |
| `gh_search` | JSON array | `["lipstick", "serum"]` | navbar search |
| `SS_deltaBuffer` | object | Smooth scroll internal state | SmoothScroll.js |

---

## 2.4 Function Map — Hàm Nào Xử Lý Gì

### `api.js` — Module chính
| Object/Fn | Mục đích |
|-----------|----------|
| `Auth.login(credentials)` | POST /api/Auth/login → lưu token+user localStorage |
| `Auth.logout()` | Xóa token, user localStorage → redirect login |
| `Auth.isLoggedIn()` | Kiểm tra `localStorage.token` |
| `Auth.isAdmin()` | Check `user.role === 'Admin'` |
| `Auth.isSeller()` | Check `user.role === 'Seller'` |
| `Auth.getToken()` | Lấy token từ localStorage |
| `Auth.getCurrentUser()` | Parse user JSON từ localStorage |
| `Product.getAll(params)` | GET /api/products với querystring |
| `Product.getById(id)` | GET /api/products/{id} |
| `Product.create(data)` | POST /api/products (Seller) |
| `Product.update(id, data)` | PUT /api/products/{id} (Seller) |
| `Cart.add({id,name,price,image})` | Nếu đăng nhập: POST /api/cart/add; Nếu không: localStorage |
| `Cart.getItems()` | Đọc gh_cart localStorage |
| `Cart._updateUI()` | Cập nhật badge số lượng trên navbar |
| `Banner.getAll()` | GET /api/Banners (active only) |
| `Featured.getBySection(section)` | GET /api/FeaturedProducts?section= |
| `FlashSale.getActive()` | GET /api/flashsale/active |
| `Category.getAll()` | GET /api/categories |
| `SiteSettings.getAll(group)` | GET /api/SiteSettings?group= |
| `SiteSettings.update(key, value)` | PUT /api/SiteSettings/{key} |
| `SiteSettings.bulkUpdate(obj)` | PUT /api/SiteSettings/bulk |
| `apiFetch(baseUrl, path, method, body)` | Core fetch wrapper với Bearer token |

### `navbar.js`
| Hàm | Mục đích |
|-----|----------|
| `renderNavUser()` | Đọc user localStorage → render avatar/tên/role label |
| `updateCartBadge(count)` | Cập nhật badge số lượng giỏ hàng |
| `updateWishlistBadge(count)` | Cập nhật badge yêu thích |
| Dropdown Categories | Load GET /api/categories → render dropdown |

### `cart-limit.js`
| Hàm | Mục đích |
|-----|----------|
| Giới hạn giỏ | Tối đa 5 loại sản phẩm trong giỏ |
| Giới hạn wishlist | Tối đa 10 SP trong wishlist |

### `index.html` inline JS
| Hàm | Mục đích |
|-----|----------|
| `addCard(btn)` | Thêm vào giỏ từ card sản phẩm |
| `_renderNewGrid(items)` | Render Asymmetric Grid 5 SP |
| `_renderBestStrip(items)` | Render strip Bán Chạy Nhất |
| `_renderSplitPanels(banners)` | Render 2 banner Split Panel |
| `_renderCategories(cats)` | Render Lifestyle Strip 3 danh mục |
| `loadIndexFlashSale()` | Async: load + render Flash Sale section |
| `loadIndexRecentlyViewed()` | Async: load + render Xem Gần Đây |
| `savePageChanges()` | Admin: SiteSettings.bulkUpdate(_pageEdits) |

---

## 2.5 Database Schema + Quan Hệ Bảng

### Bảng chính
```
Users(Id PK, UserName, Email, PasswordHash, FullName, Avatar, Role,
      IsActive, CreatedAt, OAuthProvider, OAuthId)

Shops(Id PK, SellerId FK→Users, Name, Description, Address, Phone,
      Logo, BannerUrl, Status[Pending/Active/Banned],
      CommissionRate, CreatedAt)

Categories(Id PK, Name, Description, ImageUrl[NEW], IsDeleted)

Products(Id PK, ShopId FK→Shops, CategoryId FK→Categories,
         Name, Description, Price, DiscountPrice, Stock, SoldCount,
         ImageUrl, ImageGallery[JSON], Specs[JSON], IsActive, IsNew,
         CreatedAt)

Orders(Id PK, OrderCode, UserId FK→Users, ShopId FK→Shops,
       Status[Pending/Confirmed/Shipping/Completed/Cancelled],
       TotalAmount, ShippingFee, FinalAmount, DiscountAmount,
       VoucherCode, PaymentMethod, PaymentStatus[UNPAID/PAID/FAILED],
       ShippingAddress, ReceiverName, ReceiverPhone,
       CommissionRate, ProductRevenue, CommissionAmount, SellerPayoutAmount,
       PayoutStatus[Pending/Waiting_Release/Released],
       OrderDate, EstimatedDelivery, Note)

OrderDetails(Id PK, OrderId FK→Orders, ProductId FK→Products,
             Quantity, UnitPrice)

OrderStatusHistories(Id PK, OrderId FK→Orders, Status, Note,
                     ChangedBy, ChangedAt)

CartItems(Id PK, UserId FK→Users, ProductId FK→Products,
          Quantity, AddedAt)

FlashSales(Id PK, Name, StartTime, EndTime, IsActive, CreatedAt, CreatedBy)

FlashSaleProducts(Id PK, FlashSaleId FK→FlashSales, ProductId FK→Products,
                  SalePrice, OriginalPrice, Quantity, RemainingQuantity,
                  SoldCount, IsActive)

Banners(Id PK, Title, Subtitle, ImageUrl, LinkUrl, ButtonText,
        BgColor, SortOrder, IsActive, CreatedAt, CreatedBy, UpdatedAt)

FeaturedProducts(Id PK, ProductId FK→Products, Section, SortOrder,
                 IsActive, CreatedAt)

Vouchers(Id PK, Code, Name, DiscountType[Percentage/Fixed],
         DiscountValue, MinOrderValue, MaxUsage, UsedCount,
         StartDate, EndDate, IsActive, CreatedBy)

UserVouchers(Id PK, UserId FK→Users, VoucherCode)

SellerVouchers(Id PK, ShopId FK→Shops, Code, DiscountType,
               DiscountValue, MinOrderValue, MaxUsage, UsedCount,
               StartDate, EndDate, IsActive)

Reviews(Id PK, ProductId FK→Products, UserId FK→Users, OrderId FK→Orders,
        Rating, Comment, Images[JSON], Reply, RepliedAt, CreatedAt)

QnAs(Id PK, ProductId FK→Products, UserId FK→Users, Question,
     Answer, AnsweredAt, ShopId FK→Shops)

Notifications(Id PK, UserId FK→Users, Title, Message, Link,
              IsRead, CreatedAt)

Wishlists(Id PK, CustomerId FK→Users, ProductId FK→Products, CreatedAt)

UserAddresses(Id PK, UserId FK→Users, FullName, Phone, Province,
              District, Ward, DetailAddress, IsDefault)

Disputes(Id PK, OrderId FK→Orders, UserId FK→Users,
         Title, Description, Images[JSON], Status[Open/Processing/Resolved],
         Resolution, ResolutionNote, CreatedAt, ResolvedAt)

AuditLogs(Id PK, UserId, UserName, Action, Entity, EntityId,
          OldValue[JSON], NewValue[JSON], CreatedAt)

SiteSettings(Key PK, Value, Type[text/html/image/color/bool/json],
             Group[general/homepage/seo/social], Label, UpdatedAt, UpdatedBy)

SellerWalletTransactions(Id PK, ShopId FK→Shops, Amount, Type,
                          Description, RelatedOrderId, CreatedAt)

CustomerWalletTransactions(Id PK, UserId FK→Users, Amount, Type,
                            Description, RelatedOrderId, CreatedAt)

RecentlyViewed(Id PK, UserId FK→Users, ProductId FK→Products, ViewedAt)
```

### Migrations thực hiện theo thứ tự
1. `20260318073813_InitialDatabase` — Tạo DB cơ bản
2. `20260420092352_InitialDatabase2` — Bổ sung
3. `20260428083620_AddProductFields` — IsNew, SoldCount, ImageGallery, Specs, SiteSettings, FeaturedProducts
4. `20260428084814_FixDecimalTypes` — Sửa kiểu decimal
5. `20260429134153_AddUserAddresses` — Bảng UserAddresses
6. `20260604000000_AddWishlists` — Bảng Wishlists
7. `20260611000000_AddCategoryImageUrl` — Category.ImageUrl **[NEW]**

---

## 2.6 Performance Patterns

### Atomic Flash Sale (chống race condition)
```sql
UPDATE FlashSaleProducts
SET RemainingQuantity = RemainingQuantity - @qty,
    SoldCount = SoldCount + @qty
WHERE Id = @fspId
  AND RemainingQuantity >= @qty
  AND IsActive = 1
-- rowsAffected = 0 → hết hàng
```

### Checkout Transaction
```csharp
await using var tx = await _db.Database.BeginTransactionAsync();
// 1. Re-tính giá | 2. Check stock | 3. Trừ stock | 4. Tạo Order
// 5. Tạo OrderDetails | 6. Xóa CartItems | 7. Gửi notification
await tx.CommitAsync();
// Nếu lỗi → RollbackAsync() — không có trạng thái dở dang
```

### Seller Dashboard — Song Song Hóa API Calls
```javascript
// seller-dashboard.html: Gọi song song thay vì tuần tự
Promise.all([
  fetchShopStats(),
  fetchRecentOrders(),
  fetchLowStock(),
  fetchRevenueSeries()
]).then(([stats, orders, lowStock, series]) => {
  renderAll(stats, orders, lowStock, series);
});
```

### Cloudinary Upload Flow
```javascript
POST /api/upload/image (FormData, Bearer)
  → Backend: forward lên Cloudinary API
  → cloud_name=dcucbyzdo, upload_preset=GlowHub_Upload
  → Response: { url, publicId }
  → Frontend lưu url vào form rồi submit
```

### Product Search Debounce
- Input tìm kiếm: debounce 400ms trước khi gọi API.
- Quantity update trong giỏ: debounce 400ms.

### Skeleton Loading
- Tất cả sections trang chủ có skeleton placeholder hiển thị trong khi chờ API.
- Khi data về: skeleton ẩn, content hiện với fade-in.

---

## 2.7 Luồng Dữ Liệu Từng Trang

### `index.html`
| Sự kiện | API gọi | Kết quả | localStorage |
|---------|---------|---------|--------------|
| DOMContentLoaded | `GET /api/SiteSettings?group=homepage` | Override hero img/text | — |
| DOMContentLoaded | `GET /api/Banners` | Render Split Panels | — |
| DOMContentLoaded | `GET /api/products?isNew=true&limit=5` | Render Asym Grid | — |
| DOMContentLoaded | `GET /api/products?limit=8&sort=bestseller` | Render Best Strip | — |
| DOMContentLoaded | `GET /api/categories` | Render Lifestyle + Dropdown | — |
| DOMContentLoaded | `GET /api/flashsale/active` | Hiện Flash Sale section | — |
| DOMContentLoaded | `GET /api/recently-viewed` (Bearer) | Render Recently Viewed | Đọc gh_recently_viewed |
| Click "+ Thêm giỏ" | `POST /api/cart/add` hoặc localStorage | Toast "Đã thêm" | Write gh_cart |

### `product.html`
| Sự kiện | API gọi | Kết quả | localStorage |
|---------|---------|---------|--------------|
| Load `?id=X` | `GET /api/products/X` | Render tất cả thông tin | — |
| Load | `GET /api/shops/{shopId}/profile` | Render shop mini | — |
| Load | `GET /api/orders/my/check-purchased/X` (Bearer) | Unlock/lock form review | — |
| Load | `GET /api/products/X/reviews` | Hiển thị reviews | — |
| Load | `GET /api/qna/product/X` | Hiển thị Q&A | — |
| Load | `GET /api/products?categoryId=Y&limit=4` | SP liên quan | — |
| Load | `POST /api/recently-viewed/X` (Bearer) | — | Write gh_recently_viewed |
| Click "Thêm giỏ" | `POST /api/cart/add` | Toast | Write gh_cart |
| Click heart | `POST/DELETE /api/wishlist/X` | Toggle icon | Write gh_wishlist |
| Submit review | `POST /api/upload/image` → `POST /api/products/X/reviews` | Thêm review | — |
| Submit câu hỏi | `POST /api/qna/ask` | Thêm Q&A | — |

### `shop.html`
| Sự kiện | API gọi | Kết quả | localStorage |
|---------|---------|---------|--------------|
| Load | `GET /api/categories` | Render filter danh mục | — |
| Load / Filter thay đổi | `GET /api/products?...` | Render grid SP + pagination | — |
| Search submit | `GET /api/products?keyword=X` | Lọc kết quả | Write gh_search |
| Pagination click | `GET /api/products?...&page=N` | Trang tiếp | — |

### `cart.html`
| Sự kiện | API gọi | Kết quả | localStorage |
|---------|---------|---------|--------------|
| Load | Đọc gh_cart | Render items | Read gh_cart |
| Load | `GET /api/shops/{id}/profile` (mỗi shop) | Tên shop | — |
| Qty change (+400ms debounce) | `PUT /api/cart/update` | — | Write gh_cart |
| Xóa item | `DELETE /api/cart/{id}` | Remove row | Write gh_cart |
| Nhập voucher | `POST /api/vouchers/validate` | Hiện discount | Write gh_applied_voucher |
| Checkout click | — | redirect checkout.html | Write glowhub_checkout |

### `checkout.html`
| Sự kiện | API gọi | Kết quả | localStorage |
|---------|---------|---------|--------------|
| Load | `GET /api/addresses` | Render địa chỉ | Read glowhub_checkout |
| Thêm địa chỉ | `POST /api/addresses` | Thêm vào list | — |
| Đặt mặc định | `PUT /api/addresses/{id}/set-default` | — | — |
| Đặt hàng (COD) | `POST /api/orders/checkout` | orderId | Clear gh_cart, checkout |
| Đặt hàng (VNPay) | `POST /api/orders/checkout` → `POST /api/payment/vnpay/create` | paymentUrl | — |
| Đặt hàng (CK) | `POST /api/orders/checkout` | orderId → waiting-payment | — |

### `admin.html`
| Tab | API gọi khi chuyển tab | Actions |
|-----|----------------------|---------|
| Dashboard | `/api/shops/my/dashboard` (admin stats endpoint) | — |
| Users | `GET /api/admin/users` | Ban/Unban/ChangeRole |
| Shops | `GET /api/shops/admin/all` | Approve/Ban/Commission |
| Orders | `GET /api/orders/admin/orders` | ForceStatus |
| Banners | `GET /api/Banners/all` | CRUD + Reorder |
| Flash Sale | `GET /api/flashsale/admin/list` | CRUD + AddProduct |
| Vouchers | `GET /api/vouchers` | CRUD |
| Commission | `GET /api/admin/commission/summary` | Payout |
| Disputes | `GET /api/disputes` | Process/Resolve |
| Audit Log | `GET /api/admin/audit-logs` | View only |
| Export | `GET /api/admin/export/...` | Download CSV |
| SiteSettings | `GET /api/SiteSettings/detail` | Bulk update |

---

# PHẦN 3: NHỮNG GÌ CÒN THIẾU

## 3.1 Tính Năng Chưa Implement

| # | Tính năng | Mức độ | Ghi chú |
|---|-----------|--------|---------|
| 1 | SiteSettings chưa có seed data | Cao | Cần chạy script_sprint13.sql. Hero Banner phụ thuộc |
| 2 | Category.ImageUrl chưa có seed data | Cao | Script_sprint13.sql có sẵn UPDATE cho 3 danh mục mỹ phẩm chính |
| 3 | FeaturedProducts chưa có data | Trung | Cần Admin seed SP vào section `new_arrivals` để "Hàng Mới Về" hiển thị đúng |
| 4 | Không có real-time notifications (WebSocket) | Thấp | Code WebSocket trong BaseCore.Common tồn tại nhưng chưa tích hợp vào notifications frontend |
| 5 | Chat giữa Customer ↔ Seller | Thấp | Mentioned trong docs cũ, chưa có controller/UI |
| 6 | Order tracking public (không cần login) | Trung | Controller có `track` endpoint nhưng cần public access |
| 7 | Trang `waiting-payment.html` — auto-expire | Trung | `POST /api/payment/expire` tồn tại nhưng cần cron job hoặc trigger |
| 8 | OAuth Google/Facebook | Thấp | Endpoints `/api/oauth/google`, `/api/oauth/facebook` chưa hoàn thiện |
| 9 | Admin: chỉnh SiteSettings qua UI | Trung | API sẵn sàng, UI admin.html chưa có tab SiteSettings editor |

## 3.2 Bug Đã Biết

| # | Bug | File | Mô tả |
|---|-----|------|-------|
| 1 | ~~Bán Chạy Nhất sort theo Id thay vì SoldCount~~ | index.html | **ĐÃ FIX** (commit `bc09bdc`) — thêm `sort: 'bestseller'` |
| 2 | ~~Category không có ImageUrl~~ | Category.cs | **ĐÃ FIX** (commit `bc09bdc`) — thêm field + migration |
| 3 | ~~SiteSettings không có seed data~~ | script_sprint13.sql | **ĐÃ FIX** — seed trong sprint13 |
| 4 | Lifestyle Strip ảnh hardcode theo index | index.html | Đã fix logic đọc `cat.ImageUrl`, nhưng categories hiện tại trong DB chưa có ảnh mỹ phẩm — cần chạy script_sprint13.sql |
| 5 | Cart lưu localStorage không sync server | Nhiều trang | Nếu đăng nhập nhưng dùng localStorage thay API cart → mất đồng bộ đa thiết bị |
| 6 | Flash Sale race condition đã xử lý | FlashSaleController.cs | Đã implement atomic SQL UPDATE, không phải bug |
| 7 | `compare.html` — nền đen ô Tiêu Chí | compare.html | **ĐÃ FIX** (commit `b342566`) |
| 8 | Gallery ảnh bị duplicate | product.html | **ĐÃ FIX** (commit `9f195db`) |

---

## Ghi Chú Commits Mới (Chưa Trong Tài Liệu Cũ)

| Commit | Thay đổi |
|--------|---------|
| `bc09bdc` | Fix 3 vấn đề trang chủ: bestseller sort, SiteSettings seed, Category.ImageUrl |
| `495a642` | Upload files mới (qua GitHub UI) |
| `507a6c4` đến `6e071da` | Điều chỉnh chiều cao asym grid + strip (6 commits layout) |
| `89df099` | Hero full screen + ảnh không bị cắt tỉ lệ |
| `8014723` | 3 section fit-in-viewport |
| `ddc2861` | Seller dashboard: loại ảnh chính trùng khỏi gallery |
| `9f195db` | Product: deduplicate gallery thumbnails |
| `2e5d31e` | Perf: song song hóa API calls khi load seller-dashboard |
| `ac03971` | Upload gallery nhiều ảnh (Cloudinary) |
| `27e3e83` | Tích hợp Cloudinary (cloud=dcucbyzdo, preset=GlowHub_Upload) |
| `b342566` | Compare: bỏ inline style nền đen |
| `b2c8ed9` | Compare: thêm hàng Mô Tả |
| `56b4c35` | Navbar: đồng bộ badge wishlist tất cả trang |
| `a2beccd` | Profile: wishlist layout list ngang |
| `7b504bd` | Profile: wishlist grid + navbar badge |
| `d6f8bd8` | DB: EF migration + SQL Wishlists |
