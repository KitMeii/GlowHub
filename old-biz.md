**GLOWHUB**

**👤 CUSTOMER**

**Đăng ký & Đăng nhập** Khách hàng tạo tài khoản với thông tin cơ bản,
đăng nhập nhận JWT token với role Customer. Hệ thống tự động redirect về
trang chủ sau khi đăng nhập thành công.

**Khám phá sản phẩm** Trang chủ hiển thị Flash Sale với đồng hồ đếm
ngược, sản phẩm mới về, danh mục nổi bật và sản phẩm đã xem gần đây.
Trang cửa hàng có bộ lọc nâng cao theo danh mục, khoảng giá, đánh giá,
tìm kiếm với lịch sử và gợi ý từ khóa.

**Xem sản phẩm** Trang chi tiết sản phẩm có gallery ảnh zoom, thông tin
shop mini, đánh giá có ảnh, Q&A hỏi đáp với Seller, sản phẩm liên quan.
Sticky bar xuất hiện khi scroll giúp thêm giỏ nhanh. Có thể share sản
phẩm qua Facebook, Zalo hoặc copy link.

**Trang shop public** Khách xem hồ sơ từng shop: banner, logo, thống kê
doanh số, đánh giá tổng hợp, toàn bộ sản phẩm của shop với filter và
sort. Có thể theo dõi shop hoặc chat với Seller.

**Wishlist & So sánh** Lưu sản phẩm yêu thích vào wishlist, xem lại
trong profile. So sánh tối đa 3 sản phẩm cạnh nhau về giá, rating, tồn
kho, thông số kỹ thuật --- highlight tự động ô tốt nhất.

**Giỏ hàng** Thêm sản phẩm, điều chỉnh số lượng tự động (debounce), xem
progress bar tiến đến ngưỡng miễn phí ship 500k, lưu sản phẩm sang
wishlist, xem gợi ý sản phẩm liên quan.

**Thanh toán** 3 bước: chọn địa chỉ giao hàng đã lưu hoặc nhập mới → xem
lại đơn hàng + áp voucher → chọn phương thức thanh toán (COD/MoMo/Ngân
hàng/ZaloPay). Sau khi đặt thành công hiện trang xác nhận với mã
ORD-XXXXXX.

**Theo dõi đơn hàng** Tra cứu bằng mã đơn + số điện thoại (không cần
đăng nhập) hoặc xem trong profile. Timeline 4 bước trực quan: Đặt hàng →
Xác nhận → Đang giao → Đã nhận. Có thể hủy đơn khi còn Pending hoặc xác
nhận đã nhận khi Shipping.

**Đánh giá & Q&A** Sau khi nhận hàng có thể viết review kèm ảnh, chỉ
những đơn đã giao mới được đánh giá (verified purchase). Đặt câu hỏi về
sản phẩm bất kỳ lúc nào, nhận câu trả lời từ Seller.

**Voucher** Vào kho voucher xem tất cả mã đang có hiệu lực, lưu về tài
khoản, copy code. Khi thanh toán có thể chọn voucher phù hợp từ modal,
hệ thống tự tính số tiền tiết kiệm.

**Khiếu nại** Khi nhận hàng không đúng mô tả hoặc hàng hỏng, Customer
gửi khiếu nại kèm mô tả và ảnh bằng chứng. Nhận thông báo khi Admin xử
lý và có kết quả phán quyết.

**🏪 SELLER**

**Mở shop** Đăng ký tài khoản role Seller, điền thông tin shop và gửi
yêu cầu. Shop ở trạng thái Pending cho đến khi Admin duyệt. Nhận thông
báo khi được duyệt hoặc bị từ chối.

**Dashboard tổng quan** Sau khi được duyệt, Seller vào
seller-dashboard.html xem 4 thẻ thống kê: doanh thu hôm nay, tháng này,
tổng đơn, đơn chờ xử lý. Biểu đồ doanh thu 7 ngày và danh sách SP sắp
hết hàng hiện ngay trên màn hình chính.

**Quản lý sản phẩm** Thêm/sửa/xóa sản phẩm với đầy đủ thông tin: tên, mô
tả, giá gốc, giá khuyến mãi, danh mục, tồn kho, nhiều ảnh, thông số kỹ
thuật JSON. Toggle bật/tắt hiển thị ngay trên bảng. Tìm kiếm và lọc theo
trạng thái.

**Xử lý đơn hàng** Xem tất cả đơn hàng của shop, lọc theo trạng thái.
Xác nhận đơn (Pending → Confirmed), đánh dấu đang giao (Confirmed →
Shipping), hủy đơn kèm lý do khi cần. Modal chi tiết đơn hiển thị đầy đủ
thông tin khách, địa chỉ, sản phẩm và timeline.

**Tồn kho** Xem danh sách tồn kho toàn shop, cập nhật số lượng trực
tiếp. Cảnh báo tự động khi sản phẩm còn dưới 10 đơn vị.

**Voucher shop** Tạo mã giảm giá riêng cho shop: theo % hoặc số tiền cố
định, đặt điều kiện đơn tối thiểu, giới hạn số lần dùng, thời gian hiệu
lực. Copy code nhanh bằng 1 click.

**Đánh giá & Q&A** Xem toàn bộ review của khách về sản phẩm trong shop,
phản hồi từng review (chỉ 1 lần). Trả lời câu hỏi từ khách hàng trong
tab Q&A, xem thống kê tỷ lệ phản hồi.

**Thống kê** Chọn khoảng thời gian xem doanh thu theo ngày (line chart),
top sản phẩm bán chạy (bar chart), doanh thu theo danh mục (pie chart).
Bảng chi tiết cho thấy doanh thu thô, hoa hồng phải trả và số tiền thực
nhận.

**Thông báo** Bell icon trên topbar hiển thị badge số thông báo chưa
đọc. Nhận thông báo khi có đơn mới, sản phẩm sắp hết hàng, khách để lại
review mới, khách đặt câu hỏi mới, hoặc khi đơn hàng bị hủy.

**Thông tin shop** Cập nhật tên, mô tả, địa chỉ, SĐT, logo. Xem trạng
thái duyệt, ngày tạo, tỷ lệ hoa hồng hệ thống áp dụng và số tiền thực
nhận sau khi trừ hoa hồng.

**👑 ADMIN**

**Dashboard tổng quan** Vào admin.html với giao diện đồng bộ toàn hệ
thống. 6 thẻ thống kê: tổng người dùng, tổng đơn hàng, doanh thu hôm
nay, doanh thu tháng này, tổng cửa hàng, shop chờ duyệt. Biểu đồ doanh
thu 30 ngày, top shop bán chạy, top sản phẩm bán chạy và 10 đơn hàng mới
nhất.

**Quản lý người dùng** Xem toàn bộ user, tìm kiếm và lọc theo role/trạng
thái. Khóa/mở khóa tài khoản, thay đổi role. Không thể tự đổi role của
chính mình.

**Quản lý cửa hàng** Duyệt shop mới đang Pending, khóa shop vi phạm. Xem
thống kê chi tiết từng shop: doanh thu, số đơn, đánh giá trung bình.
Điều chỉnh tỷ lệ hoa hồng riêng cho từng shop.

**Quản lý sản phẩm & danh mục** Xem toàn bộ sản phẩm trên sàn, lọc theo
shop và danh mục. Thêm/sửa/xóa danh mục, không xóa được danh mục còn sản
phẩm.

**Quản lý đơn hàng** Xem tất cả đơn toàn hệ thống, lọc theo shop/trạng
thái/thời gian. Can thiệp force-change trạng thái kèm ghi chú khi cần xử
lý tranh chấp.

**Nội dung marketing** Quản lý banner trang chủ (thêm/sửa/xóa/sắp xếp
thứ tự). Tạo và điều hành Flash Sale: chọn sản phẩm, đặt giá sale, số
lượng giới hạn, thời gian chạy. Tạo voucher toàn sàn áp dụng cho mọi đơn
hàng bất kể shop.

**Hoa hồng & Thanh toán Seller** Bảng tổng hợp hoa hồng từng shop: doanh
thu, tỷ lệ, số tiền hoa hồng thu, đã trả và còn phải trả. Ghi nhận từng
lần thanh toán cho Seller kèm số tiền, ghi chú và ngày thực hiện. Lịch
sử thanh toán đầy đủ minh bạch.

**Xử lý khiếu nại** Tiếp nhận khiếu nại từ Customer, đánh dấu đang xử
lý, xem mô tả và ảnh bằng chứng. Đưa ra phán quyết: ủng hộ khách (hoàn
tiền, trừ doanh thu Seller) hoặc ủng hộ shop (bác bỏ). Cả hai bên nhận
thông báo ngay khi có kết quả.

**Audit Log** Toàn bộ hành động quan trọng của Admin đều được ghi lại:
ai duyệt/ban shop nào, ai khóa user nào, ai thay đổi hoa hồng, ai
tạo/xóa Flash Sale. Mỗi bản ghi có trạng thái trước và sau thay đổi, dễ
dàng truy vết.

**Báo cáo & Export** Xem doanh thu tổng hệ thống theo khoảng thời gian
tùy chọn. Export CSV doanh thu, đơn hàng, danh sách user, thông tin shop
để phân tích hoặc lưu trữ hồ sơ.

**Cài đặt hệ thống** Cấu hình ngưỡng miễn phí ship, tỷ lệ hoa hồng mặc
định, thông tin liên hệ, tên website. Các thay đổi có hiệu lực ngay lập
tức trên toàn hệ thống.
