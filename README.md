# GlowHub — Tài Liệu Kỹ Thuật

> Marketplace mỹ phẩm đa shop — ASP.NET Core 8 + EF Core + SQL Server + HTML/CSS/JS

---

## MỤC LỤC

1. [Tổng Quan](#1-tổng-quan)
2. [Customer — Chi Tiết Từng Tính Năng](#2-customer--chi-tiết-từng-tính-năng)
3. [Seller — Chi Tiết Từng Tính Năng](#3-seller--chi-tiết-từng-tính-năng)
4. [Database Schema](#4-database-schema)
5. [Luồng Nghiệp Vụ Liên Kết](#5-luồng-nghiệp-vụ-liên-kết)
6. [Lưu Ý Cho Admin (Teammate)](#6-lưu-ý-cho-admin-teammate)

---

## 1. TỔNG QUAN

### Mô tả project

GlowHub là marketplace mỹ phẩm trực tuyến, cho phép nhiều shop (Seller) bán hàng trên cùng 1 nền tảng. Mỗi đơn hàng của Customer có thể chứa sản phẩm từ nhiều shop khác nhau — hệ thống tự động tách thành các **SubOrder** riêng cho từng shop.

### Công nghệ

| Layer | Công nghệ |
|---|---|
| Backend API | ASP.NET Core 8 Web API |
| ORM | Entity Framework Core 8 |
| Database | SQL Server (tên DB: `BaseCoreDB`) |
| Auth | JWT Bearer, 8 giờ, không Refresh Token |
| Frontend | HTML + CSS + Vanilla JavaScript |
| File upload | Multipart, lưu local hoặc cloud |

### Cấu trúc thư mục

```
FW/
├── BaseCore/
│   ├── BaseCore.APIService/          ← API chính (port 5001)
│   │   └── Controllers/              ← ~35 controllers
│   ├── BaseCore.AuthService/         ← Auth service (port 5002)
│   │   └── Controllers/              ← AuthController, UserController
│   ├── BaseCore.Entities/            ← 32 entity classes
│   ├── BaseCore.Repository/          ← MySqlDbContext + Repositories
│   └── BaseCore.Services/            ← Business services
├── WebClient/
│   └── public/kaira-1.0.0/          ← Frontend HTML
│       ├── js/api.js                 ← HTTP helper + API modules
│       ├── index.html                ← Trang chủ
│       ├── shop.html                 ← Danh sách sản phẩm
│       ├── product.html              ← Chi tiết sản phẩm
│       ├── shop-profile.html         ← Trang shop
│       ├── cart.html                 ← Giỏ hàng
│       ├── checkout.html             ← Thanh toán
│       ├── vouchers.html             ← Kho voucher
│       ├── profile.html              ← Hồ sơ + lịch sử đơn
│       ├── order-tracking.html       ← Theo dõi đơn
│       ├── seller-dashboard.html     ← Quản lý bán hàng
│       └── admin.html                ← Quản trị hệ thống
└── script.sql                        ← SQL Server schema đầy đủ
```

### Roles & Ports

| Role | UserType | Port |
|---|---|---|
| Customer | 0 | Gọi AuthService (5002) + APIService (5001) |
| Admin | 1 | Gọi APIService (5001) |
| Seller | 2 | Gọi APIService (5001) |

### localStorage Keys quan trọng

| Key | Mục đích |
|---|---|
| `token` | JWT Bearer token |
| `user` | Thông tin user (JSON) |
| `gh_cart` | Giỏ hàng (mảng items) |
| `gh_applied_voucher` | Voucher hệ thống đang áp dụng |
| `gh_shop_vouchers` | Map `{shopId: voucherCode}` |
| `gh_applied_ship_voucher` | Voucher freeship đang áp dụng |
| `glowhub_wishlist` | Danh sách yêu thích (mảng productId) |
| `gh_compare` | Danh sách so sánh (mảng productId) |
| `gh_followed_shops` | Shop đã follow (mảng shopId) |
| `gh_vnp` | Cache VN Provinces (24h) |

---

## 2. CUSTOMER — Chi Tiết Từng Tính Năng

### 2.1 Đăng Ký / Đăng Nhập

**File:** `login.html`, `register.html`

**Đăng ký:**
```
POST http://localhost:5002/api/Auth/register
Body: { Username, Password, Name, Email, Phone }
Response: { message: "Đăng ký thành công" }
```

**Đăng nhập:**
```
POST http://localhost:5002/api/Auth/login
Body: { Username, Password }
Response: {
  token: "eyJ...",
  userId, username, name, email, role,
  expiresIn: 28800   ← 8 giờ
}
```

**Flow sau login:**
1. Lưu `token` vào `localStorage["token"]`
2. Lưu user info vào `localStorage["user"]`
3. Redirect tùy role: Seller → `seller-dashboard.html`, Admin → `admin.html`, Customer → `index.html`

**Auto-logout:** `apiFetch()` tự detect HTTP 401 → xóa token + redirect `login.html`

---

### 2.2 Xem & Tìm Kiếm Sản Phẩm

**File:** `shop.html`

**API Calls:**
```
GET http://localhost:5001/api/products
  ?keyword=X&categoryId=Y&page=1&pageSize=12
  &minPrice=A&maxPrice=B&sort=price_asc|price_desc|newest|best_seller
  &onlyNew=true&onlySale=true

Response: {
  items: [{ id, name, price, discountPrice, imageUrl, shopId, ... }],
  totalCount, page, pageSize, totalPages
}

GET http://localhost:5001/api/categories
Response: [{ id, name, description, parentId, isDeleted }]
```

**localStorage tương tác:**
- `gh_cart` — thêm sản phẩm (không gọi API, chỉ update localStorage)
- `glowhub_wishlist` — toggle yêu thích
- `gh_compare` — thêm so sánh (max 4 sản phẩm)

---

### 2.3 Chi Tiết Sản Phẩm

**File:** `product.html`
**URL param:** `?id={productId}`

**API Calls:**
```
GET /api/products/{id}
Response: { id, name, price, discountPrice, stock, imageUrl, images,
            description, specifications, shopId, categoryId,
            avgRating, reviewCount, isActive }

GET /api/products/{id}/reviews?page=1&limit=5
Response: { items: [{ id, rating, comment, images, userId, userName,
            createdAt, sellerReply }], total, page, totalPages }

GET /api/products/{id}/qna?page=1&limit=10
Response: { items: [{ id, question, answer, customerId, createdAt }] }

GET /api/products?shopId={shopId}&pageSize=4    ← "Sản phẩm cùng shop"
GET /api/products?categoryId={catId}&pageSize=4 ← "Sản phẩm liên quan"

GET /api/orders/my/check-purchased/{id}          ← Auth required
Response: { hasPurchased: true|false }

POST /api/products/{id}/reviews                  ← Auth required
Body: { rating, comment, images }

POST /api/products/{id}/qna                      ← Auth required
Body: { question }
```

**#reviews hash flow:**
1. URL kết thúc `#reviews` → `product.html` tự click tab Đánh Giá
2. Scroll smooth đến tabs section (setTimeout 150ms)
3. Gọi `check-purchased` — nếu `hasPurchased: true` → hiện form đánh giá; ngược lại → hiện thông báo "Cần mua trước khi đánh giá"

---

### 2.4 Trang Shop

**File:** `shop-profile.html`
**URL param:** `?shopId={id}`

**API Calls:**
```
GET /api/shops/{shopId}
Response: { id, shopName, logo, description, address, phone,
            status, followCount, productCount, createdAt }

GET /api/products?shopId={shopId}&page=1&pageSize=12

GET /api/shops/{shopId}/reviews
```

---

### 2.5 Theo Dõi Shop

**File:** `shop-profile.html`

**API Calls:**
```
POST /api/shops/{shopId}/follow     ← Auth required
DELETE /api/shops/{shopId}/unfollow ← Auth required
GET /api/shops/{shopId}/follow-count
```

**localStorage:** `gh_followed_shops` — mirror client-side, cập nhật ngay khi click.

---

### 2.6 Giỏ Hàng

**File:** `cart.html`

**Đặc điểm:** Giỏ hàng lưu **100% trong localStorage**, không có Cart API.

**localStorage key:** `gh_cart`

**Cấu trúc item:**
```json
{
  "productId": 5,
  "name": "Son môi đỏ",
  "price": 250000,
  "discountPrice": 210000,
  "imageUrl": "/uploads/xxx.jpg",
  "shopId": "abc-123",
  "shopName": "Beauty Store",
  "qty": 2
}
```

**Gom nhóm theo shopId:** `cart.html` nhóm items theo `shopId` để hiển thị theo từng shop và auto-apply voucher shop riêng cho từng nhóm.

**API Calls (chỉ để validate):**
```
GET /api/products/{id}    ← Validate còn hàng + lấy giá mới nhất (N calls)
```

**Tính tổng:**
```
subtotal  = Σ(item.discountPrice ?? item.price) × item.qty
discount  = voucher system + Σ(voucher shop) + freeship
total     = subtotal - discount + shippingFee
```

---

### 2.7 Checkout

**File:** `checkout.html`

**Flow 3 bước:**
```
Bước 1: Chọn địa chỉ giao hàng
  → GET /api/address                 ← Danh sách địa chỉ đã lưu
  → POST /api/address                ← Thêm địa chỉ mới nếu cần

Bước 2: Chọn voucher & phương thức thanh toán
  → GET /api/vouchers/public         ← Tất cả voucher active
  → GET /api/vouchers/my             ← Voucher đã lưu (auth)

Bước 3: Xác nhận & đặt hàng
  → POST /api/orders/checkout
```

**Voucher 3 loại:**

| Loại | localStorage key | Giảm cho |
|---|---|---|
| Hệ thống | `gh_applied_voucher` | Tổng đơn hàng |
| Theo shop | `gh_shop_vouchers` (map) | Từng shop riêng |
| Freeship | `gh_applied_ship_voucher` | Phí vận chuyển |

**Payload đặt hàng đầy đủ:**
```json
POST /api/orders/checkout
{
  "shippingAddress": "123 Lê Lợi, Q1, TP.HCM",
  "receiverName": "Nguyễn Văn A",
  "receiverPhone": "0901234567",
  "toProvince": "Hồ Chí Minh",
  "note": "Giao giờ hành chính",
  "paymentMethod": 0,
  "shippingMethod": "standard|express|same",
  "voucherCode": "GLOWHUB10",
  "shipVoucherCode": "FREESHIP50",
  "shopVouchers": [
    { "shopId": "abc-123", "voucherCode": "SHOP20" }
  ]
}

Response: {
  orderId, orderCode, status, finalAmount,
  paymentMethod, paymentStatus,
  subOrders: [{ shopId, shopName, finalAmount, items }],
  vnpayUrl: "https://..."   // chỉ khi paymentMethod=VNPAY
}
```

**Sau đặt hàng thành công:**
- Xóa `gh_cart`, `gh_applied_voucher`, `gh_shop_vouchers`, `gh_applied_ship_voucher`
- Redirect `order-tracking.html?orderId={id}`

**Phương thức thanh toán:**

| Giá trị | Tên |
|---|---|
| `0` | COD (thanh toán khi nhận) |
| `1` | Bank Transfer (Admin duyệt thủ công) |
| `4` | VNPay (redirect sang cổng thanh toán) |

---

### 2.8 Voucher

**File:** `vouchers.html`

**Tabs:** Tất Cả / Hệ Thống / Theo Shop / Đã Lưu

**API Calls:**
```
GET /api/vouchers/public         ← Tất cả voucher active (không auth)
GET /api/vouchers/my             ← Voucher đã lưu (auth)

POST /api/vouchers/{id}/save     ← Lưu voucher (auth)
Response: { message: "Đã lưu voucher" }
```

**Cấu trúc voucher:**
```json
{
  "id": 1,
  "code": "GLOWHUB10",
  "description": "Giảm 10% tối đa 50k",
  "discountType": "percent|fixed",
  "discountValue": 10,
  "maxDiscount": 50000,
  "minOrderAmount": 200000,
  "expiryDate": "2026-12-31T00:00:00Z",
  "shopId": null,
  "voucherType": "system|shop"
}
```

**Logic:** `CustomerVouchers` table — unique (UserId, VoucherId). `IsUsed = true` sau khi dùng.

---

### 2.9 Theo Dõi Đơn Hàng

**File:** `order-tracking.html`
**URL param:** `?orderId={id}`

**API Calls:**
```
GET /api/orders/my/{orderId}
Response: {
  orderId, orderCode, status, finalAmount,
  shippingAddress, receiverName, receiverPhone,
  paymentMethod, paymentStatus,
  subOrders: [{
    subOrderId, shopId, shopName, status,
    items: [{ productId, productName, imageUrl, qty, unitPrice }]
  }],
  statusHistory: [{ status, note, changedAt }]
}

POST /api/orders/my/{orderId}/received
Body: { subOrderId: 5 }              ← Xác nhận từng shop riêng
Response: { message: "Đã xác nhận nhận hàng cho shop X" }
```

**Nút hành động theo trạng thái SubOrder:**
- `SHIPPING` → nút xanh **"✓ Đã Nhận Hàng"** → gọi API received với subOrderId
- `DELIVERED` → nút hồng **"⭐ Đánh Giá Sản Phẩm"** dưới từng sản phẩm → link `product.html?id={pid}#reviews`

**Banner khi hoàn tất:** Tất cả SubOrder = `DELIVERED` → `"🎉 Bạn đã nhận đủ hàng! Hãy đánh giá để giúp người mua khác nhé!"`

---

### 2.10 Profile

**File:** `profile.html`

**API Calls:**
```
GET /api/auth/me                                ← Thông tin user
PUT /api/users/profile                          ← Cập nhật profile

GET /api/orders/my?page=1&limit=10&status=X    ← Lịch sử đơn (phân trang)
GET /api/orders/my/{orderId}                    ← Chi tiết đơn

POST /api/orders/my/{orderId}/cancel            ← Hủy đơn (chỉ PENDING)
Body: { reason: "Lý do hủy" }

GET /api/address                                ← Danh sách địa chỉ
POST /api/address
PUT /api/address/{id}
DELETE /api/address/{id}

GET /api/wallet/my                              ← Ví customer
GET /api/vouchers/my                            ← Voucher đã lưu
```

**Tab đơn hàng:** Filter theo status: Tất Cả / Chờ xử lý / Đã xác nhận / Đang giao / Đã giao / Đã hủy.

**Chi tiết đơn hàng** hiển thị per-shop cards với SubOrder status và nút xác nhận/đánh giá.

---

### 2.11 Flash Sale

**File:** `index.html` (section), `flash-sale.html` (trang riêng)

**API Calls:**
```
GET /api/flashsale/active
Response: {
  id, name, startTime, endTime, secondsRemaining,
  products: [{
    productId, name, image, originalPrice, salePrice,
    discountPercent, quantity, soldCount, remainingQuantity
  }]
}
```

**Atomic stock decrement (safe race condition):**
```sql
UPDATE FlashSaleProducts
SET RemainingQuantity = RemainingQuantity - @qty
WHERE FlashSaleProductId = @id AND RemainingQuantity >= @qty
```

---

### 2.12 Đánh Giá Sản Phẩm

**File:** `product.html` (tab Đánh Giá)

**Điều kiện hiển thị form:** Chỉ user đã mua sản phẩm (`hasPurchased = true`).

**Check purchase:**
```
GET /api/orders/my/check-purchased/{productId}   ← Auth required
Response: { hasPurchased: true }
```

**Gửi đánh giá:**
```
POST /api/products/{id}/reviews                  ← Auth required
Body: { rating: 5, comment: "Sản phẩm tốt!", images: ["url1"] }
```

**Constraint DB:** Unique index (UserId, ProductId) — 1 user chỉ review 1 sản phẩm 1 lần.

---

## 3. SELLER — Chi Tiết Từng Tính Năng

**File:** `seller-dashboard.html`
**Auth:** JWT với Role = "Seller" (`UserType = 2`)

---

### 3.1 Tab Tổng Quan

```
GET /api/shops/my/dashboard
Response: {
  shopId, shopName, status,
  todayOrders, todayRevenue, monthRevenue, totalRevenue,
  productCount, pendingOrders,
  revenueChart: [{ date, orders, revenue }],
  recentOrders: [{ subOrderId, orderCode, status, finalAmount }]
}

GET /api/shops/my     ← Thông tin shop đầy đủ
```

---

### 3.2 Tab Sản Phẩm — CRUD

```
GET /api/products?shopId={myShopId}&page=1&pageSize=20&keyword=X

POST /api/products
Body: {
  name, price, discountPrice, stock, categoryId,
  description, specifications, imageUrl, images,
  weightGram, isActive
}

PUT /api/products/{id}
DELETE /api/products/{id}    ← Soft delete: IsActive = false

POST /api/upload/product-image   ← Multipart/form-data
Response: { url: "/uploads/products/xxx.jpg" }
```

---

### 3.3 Tab Đơn Hàng — SubOrder Flow

```
GET /api/seller/orders?page=1&limit=20&status=PENDING
SubOrder response: {
  id, subOrderCode, orderId, shopId, status,
  totalAmount, shippingFee, finalAmount,
  productRevenue, commissionRate, commissionAmount,
  sellerPayoutAmount, payoutStatus, trackingCode,
  items: [{ productId, name, qty, unitPrice }]
}

PUT /api/seller/orders/{subOrderId}/confirm    ← PENDING → CONFIRMED
PUT /api/seller/orders/{subOrderId}/ship       ← CONFIRMED → SHIPPING
  Body: { trackingCode: "GHN123456" }
PUT /api/seller/orders/{subOrderId}/cancel     ← PENDING → CANCELLED
  Body: { reason: "Hết hàng" }
```

**SubOrder Status Flow:**
```
PENDING → [Seller confirm] → CONFIRMED → [Seller ship] → SHIPPING
  → [Customer confirm] → DELIVERED → [Admin release] → (PayoutStatus: RELEASED)
```

---

### 3.4 Tab Voucher Shop — CRUD

```
GET /api/seller/vouchers?page=1&limit=20

POST /api/seller/vouchers
Body: {
  code, description, discountType, discountValue,
  maxDiscount, minOrderAmount, startDate, expiryDate,
  usageLimit, isActive
}

PUT /api/seller/vouchers/{id}
DELETE /api/seller/vouchers/{id}
```

`Voucher.ShopId = myShopId` — voucher chỉ dùng được cho shop này.

---

### 3.5 Tab Đánh Giá — Xem + Phản Hồi

```
GET /api/seller/reviews?page=1&limit=20&productId=X&rating=Y
Response: {
  items: [{ id, productId, productName, userId, userName,
            rating, comment, images, sellerReply, createdAt }]
}

POST /api/seller/reviews/{id}/reply
Body: { reply: "Cảm ơn bạn đã đánh giá!" }
```

---

### 3.6 Tab Thống Kê — Doanh Thu 7 Ngày

```
GET /api/reports/seller/summary
Response: {
  totalRevenue, totalOrders, totalProducts, avgOrderValue,
  totalCommission, totalNetRevenue,
  topCategory, walletBalance, walletPending
}

GET /api/reports/seller/revenue?from=2026-05-01&to=2026-05-31
Response: [{ date, totalOrders, revenue, commission, netRevenue }]

GET /api/reports/seller/products?limit=10
Response: [{ productId, name, soldCount, revenue, avgRating }]
```

---

### 3.7 Tab Ví & Thanh Toán

```
GET /api/seller/wallet
Response: {
  balance, totalEarned, totalWithdrawn, totalRefunded,
  pendingPayout,     ← SUM(SellerPayoutAmount) WHERE PayoutStatus=WAITING_RELEASE
  waitingCount
}

GET /api/seller/wallet/transactions?type=EARNING|WITHDRAWAL&page=1&limit=20
Response: {
  items: [{ id, type, amount, balanceBefore, balanceAfter, note, createdAt }]
}
```

**Công thức:**
```
ProductRevenue      = Σ(unitPrice × qty)
CommissionAmount    = ProductRevenue × CommissionRate / 100
SellerPayoutAmount  = ProductRevenue - CommissionAmount
ShopVoucherDiscount = Giảm giá voucher shop (Seller chịu chi phí)

Khi Admin giải ngân:
  Wallet.Balance += SellerPayoutAmount
```

**Chờ giải ngân:**
```sql
SELECT SUM(SellerPayoutAmount)
FROM SubOrders
WHERE ShopId = @shopId AND PayoutStatus = 'WAITING_RELEASE'
```

---

### 3.8 Tab Chat Khách Hàng

- localStorage key: `gh_chat_{shopId}` — lưu lịch sử chat local
- Chưa có WebSocket — hiện tại là mock data từ localStorage

---

### 3.9 Tab Hỏi Đáp

```
GET /api/seller/qna?page=1&limit=20&answered=false
Response: {
  items: [{ id, productId, productName, question,
            answer, customerId, createdAt }]
}

POST /api/seller/qna/{id}/reply
Body: { answer: "Sản phẩm phù hợp với mọi loại da" }
```

---

## 4. DATABASE SCHEMA

### Sơ đồ quan hệ chính

```
Users (Id: nvarchar(450) PK)
  ├─1:N─→ Orders          (UserId FK RESTRICT)
  ├─1:N─→ Reviews         (UserId FK RESTRICT)
  ├─1:N─→ CustomerVouchers(UserId FK CASCADE)
  ├─1:N─→ Notifications   (UserId FK CASCADE)
  ├─1:1─→ CustomerWallet  (UserId PK/FK CASCADE)
  └─1:N─→ Shops           (SellerId FK RESTRICT)

Orders (Id: int PK)
  ├─1:N─→ SubOrders       (OrderId FK CASCADE)
  ├─1:N─→ OrderDetails    (OrderId FK CASCADE)
  └─1:N─→ OrderStatusHistories (OrderId FK CASCADE)

SubOrders (Id: int PK)
  ├─N:1─→ Shops           (ShopId FK NO ACTION)
  └─1:N─→ SubOrderItems   (SubOrderId FK CASCADE)
             └─N:1─→ Products (ProductId FK RESTRICT)

Shops (Id: nvarchar(450) PK)
  ├─1:1─→ SellerWallet    (ShopId PK/FK CASCADE)
  └─1:N─→ WalletTransactions (ShopId FK CASCADE)

Products (Id: int PK)
  ├─N:1─→ Categories      (CategoryId FK RESTRICT)
  ├─N:1─→ Shops           (ShopId FK SET NULL)
  └─1:N─→ FlashSaleProducts (ProductId FK RESTRICT)

Vouchers (Id: int PK, Code UNIQUE)
  └─N:1─→ Shops           (ShopId FK SET NULL, null = hệ thống)
```

### Bảng Entity — Fields Quan Trọng

**Users**
```sql
Id            nvarchar(450) PK
UserName      nvarchar(50)  UNIQUE NOT NULL
Password      nvarchar(255) NOT NULL
Name          nvarchar(100)
Email         nvarchar(100)
Phone         nvarchar(20)
UserType      int  DEFAULT 0   -- 0=Customer, 1=Admin, 2=Seller
IsActive      bit  DEFAULT 1
OAuthProvider nvarchar(20)     -- Google/Facebook
OAuthId       nvarchar(200)
Created       datetime2
```

**Shops**
```sql
Id             nvarchar(450) PK
SellerId       nvarchar(450) FK→Users (RESTRICT)
ShopName       nvarchar(100) NOT NULL
CommissionRate decimal(5,2)  -- % hoa hồng (vd: 10.00 = 10%)
Logo           nvarchar(500)
Province       nvarchar(100)
Region         nvarchar(20)  -- NORTH|SOUTH|CENTRAL|ISLAND|OTHER
Status         nvarchar(20)  -- PENDING|ACTIVE|BANNED
```

**Products**
```sql
Id             int IDENTITY PK
Name           nvarchar(200) NOT NULL
Price          decimal(18,2)
DiscountPrice  decimal(18,2) NULL
Stock          int           DEFAULT 0
ImageUrl       nvarchar(500)
Images         nvarchar(2000) -- JSON array URLs
Specifications nvarchar(2000) -- JSON key-value
CategoryId     int FK→Categories (RESTRICT)
ShopId         nvarchar(450) FK→Shops (SET NULL)
IsActive       bit           DEFAULT 1
WeightGram     int           DEFAULT 500
RowVersion     timestamp     -- Optimistic concurrency token
```

**Orders**
```sql
Id                    int IDENTITY PK
OrderCode             nvarchar(20)  -- "ORD-000001"
UserId                nvarchar(450) FK→Users (RESTRICT)
ShopId                nvarchar(450) NULL FK→Shops (SET NULL)
Status                nvarchar(20)  DEFAULT 'PENDING'
PaymentMethod         nvarchar(20)  -- COD|BANK|VNPAY
PaymentStatus         nvarchar(20)  -- UNPAID|WAITING_PAYMENT|PAID|REFUNDED
TotalAmount           decimal(18,2)
ShippingFee           decimal(18,2)
FinalAmount           decimal(18,2)
SystemVoucherDiscount decimal(18,2) DEFAULT 0
ShopVoucherDiscount   decimal(18,2) DEFAULT 0
FreeshipDiscount      decimal(18,2) DEFAULT 0
PayoutStatus          nvarchar(20)  DEFAULT 'PENDING'
VNPayTransactionId    nvarchar(100) -- Idempotency key cho VNPay IPN
PaymentExpireAt       datetime2 NULL
ReceiverName          nvarchar(100)
ReceiverPhone         nvarchar(20)
ShippingAddress       nvarchar(500)
ToProvince            nvarchar(100)
OrderDate             datetime2
EstimatedDelivery     datetime2 NULL
```

**SubOrders**
```sql
Id                  int IDENTITY PK
SubOrderCode        nvarchar(20)  -- "SUB-000001"
OrderId             int FK→Orders (CASCADE)
ShopId              nvarchar(450) FK→Shops (NO ACTION)
Status              nvarchar(20)  DEFAULT 'PENDING'
TotalAmount         decimal(18,2)
ShippingFee         decimal(18,2)
FinalAmount         decimal(18,2)
ProductRevenue      decimal(18,2) -- Doanh thu gộp trước commission
CommissionRate      decimal(5,2)
CommissionAmount    decimal(18,2)
SellerPayoutAmount  decimal(18,2) -- Seller thực nhận
ShopVoucherDiscount decimal(18,2)
PayoutStatus        nvarchar(20)  DEFAULT 'PENDING'
TrackingCode        nvarchar(100)
CancelReason        nvarchar(500)
ToProvince          nvarchar(100)
CreatedAt           datetime2
```

**SubOrderItems**
```sql
Id         int IDENTITY PK
SubOrderId int FK→SubOrders (CASCADE)
ProductId  int FK→Products (RESTRICT)
Quantity   int           DEFAULT 1
UnitPrice  decimal(18,2)
```

**Vouchers**
```sql
Id             int IDENTITY PK
Code           nvarchar(50) UNIQUE NOT NULL
DiscountType   nvarchar(20)  -- percent|fixed
DiscountValue  decimal(18,2)
MaxDiscount    decimal(18,2) NULL
MinOrderAmount decimal(18,2) DEFAULT 0
UsageLimit     int NULL      -- null = không giới hạn
UsedCount      int DEFAULT 0
StartDate      datetime2 NULL
ExpiryDate     datetime2 NULL
IsActive       bit DEFAULT 1
ShopId         nvarchar(450) NULL FK→Shops (SET NULL)
               -- null = hệ thống/freeship, có giá trị = shop voucher
```

**CustomerVouchers**
```sql
Id        int IDENTITY PK
UserId    nvarchar(450) FK→Users (CASCADE)
VoucherId int FK→Vouchers (CASCADE)
IsUsed    bit DEFAULT 0
SavedAt   datetime2
UNIQUE (UserId, VoucherId)
```

**SellerWallets**
```sql
ShopId         nvarchar(450) PK FK→Shops (CASCADE)
Balance        decimal(18,2) DEFAULT 0
TotalEarned    decimal(18,2) DEFAULT 0
TotalWithdrawn decimal(18,2) DEFAULT 0
TotalRefunded  decimal(18,2) DEFAULT 0
UpdatedAt      datetime2
```

**WalletTransactions** (ledger bất biến)
```sql
Id            int IDENTITY PK
ShopId        nvarchar(450) FK→Shops (CASCADE)
OrderId       int NULL FK→Orders (NO ACTION)
Type          nvarchar(20)  -- EARNING|WITHDRAWAL|REFUND
Amount        decimal(18,2)
BalanceBefore decimal(18,2) -- Số dư trước giao dịch
BalanceAfter  decimal(18,2) -- = BalanceBefore + Amount
Note          nvarchar(500)
CreatedAt     datetime2
```

**FlashSales / FlashSaleProducts**
```sql
FlashSales:
  Id int PK, Name nvarchar(200), StartTime datetime2,
  EndTime datetime2, IsActive bit

FlashSaleProducts:
  Id int PK, FlashSaleId int FK→FlashSales (CASCADE),
  ProductId int FK→Products (RESTRICT),
  SalePrice decimal, OriginalPrice decimal,
  Quantity int, SoldCount int DEFAULT 0,
  RemainingQuantity int DEFAULT 0, IsActive bit
```

**Reviews**
```sql
Id           int IDENTITY PK
ProductId    int FK→Products (CASCADE)
UserId       nvarchar(450) FK→Users (RESTRICT)
Rating       int  -- 1 đến 5
Comment      nvarchar(1000)
Images       nvarchar(1000)
SellerReply  nvarchar(500) NULL
IsVerifiedPurchase bit DEFAULT 0
CreatedAt    datetime2
UNIQUE (UserId, ProductId)
```

**ShopFollows**
```sql
Id        int IDENTITY PK
UserId    nvarchar(450) FK→Users (CASCADE)
ShopId    nvarchar(450) FK→Shops (CASCADE)
CreatedAt datetime2
UNIQUE (UserId, ShopId)
```

**CustomerWallets / CustomerWalletTransactions**
```sql
CustomerWallets:
  UserId PK FK→Users (CASCADE), Balance, TotalReceived, TotalSpent

CustomerWalletTransactions:
  Id PK, UserId FK, Type nvarchar(20),
  Amount, BalanceBefore, BalanceAfter, Note, OrderId FK, CreatedAt
```

**Disputes**
```sql
Id           int IDENTITY PK
OrderId      int FK→Orders (RESTRICT)
CustomerId   nvarchar(450) FK→Users (RESTRICT)
Reason       nvarchar(200)
Status       nvarchar(20)  -- OPEN|PROCESSING|RESOLVED|REJECTED
RefundAmount decimal(18,2) NULL
ResolvedBy   nvarchar(450) NULL
CreatedAt    datetime2
```

**AuditLogs**
```sql
Id         int IDENTITY PK
UserId     nvarchar(450) NULL
UserName   nvarchar(256) NULL
Action     nvarchar(100) -- LOGIN|PRODUCT_CREATE|COMMISSION_PAYOUT...
Entity     nvarchar(100) NULL
EntityId   nvarchar(450) NULL
OldValues  nvarchar(max) NULL  -- JSON
NewValues  nvarchar(max) NULL  -- JSON
IpAddress  nvarchar(50) NULL
CreatedAt  datetime2
```

---

## 5. LUỒNG NGHIỆP VỤ LIÊN KẾT

### 5.1 Đặt Hàng → Giải Ngân (Từng Bước)

```
Bước 1: Customer đặt hàng
   POST /api/orders/checkout
   → Validate stock từng sản phẩm
   → Tạo Order (Status=PENDING, PayoutStatus=PENDING)
   → Group by ShopId → Tạo SubOrder per shop
   → Tính ShippingFee động (theo vùng + trọng lượng)
   → Áp voucher hệ thống / shop / freeship
   → Trừ Product.Stock
   → CustomerVoucher.IsUsed = true

Bước 2: Seller xác nhận
   PUT /api/seller/orders/{subOrderId}/confirm
   → SubOrder.Status = CONFIRMED

Bước 3: Seller giao hàng
   PUT /api/seller/orders/{subOrderId}/ship
   Body: { trackingCode }
   → SubOrder.Status = SHIPPING

Bước 4: Customer xác nhận nhận hàng (PER SHOP)
   POST /api/orders/my/{orderId}/received  Body: { subOrderId }
   → SubOrder.Status = DELIVERED
   → SubOrder.PayoutStatus = WAITING_RELEASE
   → Nếu TẤT CẢ SubOrder = DELIVERED → Order.Status = COMPLETED

Bước 5: Admin giải ngân (batch)
   POST /api/admin/wallet/release-payouts
   → Foreach SubOrder WHERE PayoutStatus = WAITING_RELEASE:
       BalanceBefore = wallet.Balance
       BalanceAfter  = BalanceBefore + sub.SellerPayoutAmount
       INSERT WalletTransaction (immutable ledger)
       wallet.Balance = BalanceAfter
       sub.PayoutStatus = RELEASED
```

### 5.2 Voucher: Tạo → Lưu → Áp Dụng → Ai Chịu Chi Phí

```
Tạo:
  Admin → Voucher hệ thống (ShopId=null) / Freeship (ShopId=null)
  Seller → Voucher shop (ShopId=myShopId)

Lưu vào tài khoản:
  POST /api/vouchers/{id}/save
  → INSERT CustomerVouchers (UserId, VoucherId, IsUsed=false)

Áp dụng tại checkout:
  System voucher → giảm Order.TotalAmount   → Platform chịu chi phí
  Shop voucher   → giảm SubOrder.FinalAmount → Seller chịu (trừ vào SellerPayoutAmount)
  Freeship       → giảm ShippingFee          → Platform chịu chi phí

Sau đặt hàng:
  CustomerVoucher.IsUsed = true
  Voucher.UsedCount++
```

### 5.3 Trạng Thái Đơn: Customer → Seller → Admin

```
[Customer đặt]    Order.Status = PENDING
[Seller confirm]  SubOrder.Status = CONFIRMED
[Seller ship]     SubOrder.Status = SHIPPING
[Customer nhận]   SubOrder.Status = DELIVERED  (per shop)
                  SubOrder.PayoutStatus = WAITING_RELEASE
[All delivered]   Order.Status = COMPLETED
[Admin payout]    SubOrder.PayoutStatus = RELEASED
                  WalletTransaction ghi nhận

[Customer hủy]    Order.Status = CANCELLED  (chỉ từ PENDING, bắt buộc lý do)
[Seller hủy]      SubOrder.Status = CANCELLED (chỉ PENDING SubOrder)
```

### 5.4 Đánh Giá: Nhận Hàng → Đánh Giá → Phản Hồi

```
1. Customer xác nhận nhận (SubOrder → DELIVERED)
2. Nút ⭐ Đánh Giá xuất hiện dưới từng sản phẩm trong order-tracking + profile
3. Click → product.html?id={pid}#reviews (auto-scroll + mở form)
4. Gọi GET check-purchased → form hiện nếu hasPurchased = true
5. Customer gửi đánh giá (rating 1-5 + comment + ảnh)
6. Seller thấy trong tab Đánh Giá của Dashboard
7. Seller POST reply → Review.SellerReply được cập nhật
8. Phản hồi hiển thị dưới comment trên product.html
```

---

## 6. LƯU Ý CHO ADMIN (Teammate đọc để làm phần admin)

### SubOrder vs Order — QUAN TRỌNG NHẤT

**Order.ShopId = null** đối với đơn hàng từ nhiều shop:

```csharp
// SAI — bỏ sót đơn đa-shop:
var revenue = await _db.Orders
    .Where(o => o.ShopId == shopId)
    .SumAsync(o => o.TotalAmount);

// ĐÚNG — luôn dùng SubOrders:
var revenue = await _db.SubOrders
    .Where(s => s.ShopId == shopId && s.Order.Status == OrderStatus.Completed)
    .SumAsync(s => s.ProductRevenue);
```

### Payout Loop — Code mẫu đúng

```csharp
foreach (var sub in waitingSubOrders.GroupBy(s => s.ShopId!))
{
    var wallet = await GetOrCreateWallet(sub.Key);
    var runningBalance = wallet.Balance;

    foreach (var item in sub)
    {
        var before = runningBalance;
        var after  = before + item.SellerPayoutAmount;
        _db.WalletTransactions.Add(new WalletTransaction {
            BalanceBefore = before,
            Amount        = item.SellerPayoutAmount,
            BalanceAfter  = after
        });
        item.PayoutStatus = PayoutStatusValue.Released;
        runningBalance    = after;
    }

    wallet.Balance      = runningBalance;
    wallet.TotalEarned += sub.Sum(s => s.SellerPayoutAmount);
}
await _db.SaveChangesAsync();
```

### Performance — KHÔNG dùng ToListAsync() không giới hạn

```csharp
// SAI (load cả table vào RAM):
var all = await _db.Orders.ToListAsync();
var revenue = all.Sum(o => o.FinalAmount);

// ĐÚNG:
var revenue = await _db.Orders
    .Where(o => o.Status == OrderStatus.Completed)
    .SumAsync(o => o.FinalAmount);

// Pagination đúng:
var items = await _db.Orders
    .OrderByDescending(o => o.OrderDate)
    .Skip((page - 1) * limit)
    .Take(limit)
    .ToListAsync();
```

### Trạng Thái Tiếng Việt

| Code | Tiếng Việt |
|---|---|
| `PENDING` | Chờ Xử Lý |
| `CONFIRMED` | Đã Xác Nhận |
| `SHIPPING` | Đang Giao |
| `DELIVERED` | Đã Giao |
| `COMPLETED` | Hoàn Thành |
| `CANCELLED` | Đã Hủy |
| `WAITING_RELEASE` | Chờ Giải Ngân |
| `RELEASED` | Đã Giải Ngân |
| `WAITING_PAYMENT` | Chờ Thanh Toán |
| `PAID` | Đã Thanh Toán |

### API Admin Cần Làm — 16 Tab

| Tab | Endpoints chính |
|---|---|
| Dashboard | `GET /api/admin/dashboard` · `GET /api/admin/stats/summary` |
| Người dùng | `GET /api/admin/users?page=&role=` · `PUT /api/admin/users/{id}/ban` |
| Shop | `GET /api/admin/shops?status=pending` · `PUT .../approve` · `PUT .../ban` |
| Sản phẩm | `GET /api/admin/products` · `PUT .../hide` · `DELETE .../` |
| Đơn hàng | `GET /api/admin/orders?status=&from=&to=` · `PUT .../cancel` |
| Chuyển khoản | `GET /api/admin/bank-transfers?status=pending` · `PUT .../confirm` · `PUT .../reject` |
| Danh mục | `GET/POST/PUT/DELETE /api/categories` (soft delete: `IsDeleted=true`) |
| Banner | `GET/POST/PUT/DELETE /api/banners` |
| Flash Sale | `GET/POST/PUT/DELETE /api/flashsale` · `POST /api/flashsale/{id}/products` |
| Voucher HT | `GET/POST/PUT/DELETE /api/admin/vouchers?type=system\|freeship` |
| Doanh thu | `GET /api/admin/stats/revenue?from=&to=&groupBy=day\|month` |
| Hoa hồng | `GET /api/admin/commission/summary` · `GET /api/admin/commission/shops` |
| Ví | `GET /api/admin/wallet/overview` · `POST /api/admin/wallet/release-payouts` |
| Audit Log | `GET /api/admin/audit-logs?userId=&action=&from=&to=` |
| Cài đặt | `GET /api/admin/settings` · `PUT /api/admin/settings` |
| Đánh giá | `GET /api/admin/reviews` · `DELETE /api/admin/reviews/{id}` |

### Constants Quan Trọng (copy vào code admin)

```csharp
// PayoutStatus
"PENDING"         // Chờ xác nhận nhận hàng
"WAITING_RELEASE" // Customer đã nhận, chờ Admin giải ngân
"RELEASED"        // Đã chuyển vào ví Seller
"REFUNDED"        // Đã hoàn tiền

// PaymentStatus
"UNPAID"           // COD chưa thu
"WAITING_PAYMENT"  // VNPay/Bank chưa thanh toán
"PAID"             // Đã thanh toán
"REFUNDED"         // Đã hoàn tiền

// WalletTransaction.Type
"EARNING"    // Giải ngân từ đơn hàng
"WITHDRAWAL" // Rút tiền ra
"REFUND"     // Hoàn tiền
```

---

*README được tạo tự động từ source code GlowHub — 2026-06-04*
