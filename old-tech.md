HỆ THỐNG BÁN MỸ PHẨM TRỰC TUYỂN GLOWHUB

**Slogan: Your Beauty, Your Glow**

**I. TẦM NHÌN VÀ TRIẾT LÝ VẬN HÀNH: NGHỆ THUẬT GIAO THOA CÔNG NGHỆ**

Hệ thống không chỉ đơn thuần là một cửa hàng trực tuyến trưng bày sản
phẩm, mà được định vị là một nền tảng vận hành kinh doanh thực thụ.
Triết lý cốt lõi của dự án nằm ở sự kết hợp hoàn mỹ giữa vẻ đẹp tinh tế
của ngành mỹ phẩm và sự khắt khe, chặt chẽ của một hệ thống quản lý giao
dịch tài chính.

Mục tiêu trọng tâm: Giải quyết triệt để bài toán \"Tồn kho thực tế\" và
\"Giao dịch an toàn\". Bằng cách kế thừa tư duy từ các nền tảng đặt chỗ
(Booking) chuyên nghiệp, hệ thống đảm bảo rằng mỗi đơn hàng khi được
khởi tạo là một cam kết chắc chắn về nguồn cung, loại bỏ hoàn toàn tình
trạng sai lệch dữ liệu và mang lại niềm tin tuyệt đối cho người tiêu
dùng.

**II. KIẾN TRÚC HỆ THỐNG: SỨC MẠNH CỦA SỰ PHÂN TÁCH (DECOUPLED
ARCHITECTURE)**

Hệ thống vận hành theo **mô hình Client-Server** bóc tách hoàn toàn, dựa
trên giải pháp **BaseCore** vững chắc, giúp tối ưu hóa hiệu năng và khả
năng bảo trì:

- **Backend (Bộ não điều hành):** Xây dựng trên nền tảng **ASP.NET Core
  Web API** với cấu trúc phân lớp (Layered Architecture). Mỗi lớp đảm
  nhiệm một vai trò chuyên biệt: Controller điều phối yêu cầu, Service
  xử lý logic nghiệp vụ tinh vi, và Repository giao tiếp an toàn với
  Database thông qua Entity Framework Core.

- **Frontend (Trải nghiệm người dùng):** Khai thác tối đa sự sang trọng
  của **Template HTML Kaira**, tích hợp linh hoạt vào môi trường
  **Vite + Reac**t. Dữ liệu được **truyền tải động qua API**, tạo ra một
  hành trình mua sắm mượt mà, phản hồi tức thì và hiện đại.

- **Database (Trái tim lưu trữ):** **SQL Server** đóng vai trò là kho
  lưu trữ bền vững. Mọi biến động về dòng tiền hay hàng hóa đều được bảo
  vệ bởi Database Transaction, đảm bảo tính toàn vẹn dữ liệu tuyệt đối
  (ACID).

**III. THIẾT KẾ CƠ SỞ DỮ LIỆU: NỀN TẢNG CỦA SỰ MINH BẠCH**

Dựa trên tệp tin **script.sql hiện có**, hệ thống quản lý dữ liệu thông
qua các nhóm bảng có mối quan hệ hữu cơ:

- **Nhóm Sản phẩm (Categories, Products):** Ngoài các thông tin định
  danh và mô tả, trọng tâm nằm ở cột StockQuantity. Đặc biệt, cơ chế
  Optimistic Concurrency được triển khai qua cột RowVersion (Timestamp)
  để ngăn chặn mọi xung đột khi có hàng nghìn lượt truy cập mua sắm cùng
  lúc.

- **Nhóm Người dùng (Users, Roles):** Thiết lập ranh giới quyền hạn
  nghiêm ngặt. Hệ thống phân định rõ quyền năng của Admin (quản trị kho,
  duyệt đơn) và Khách hàng (trải nghiệm, đặt hàng).

- **Nhóm Giỏ hàng & Đơn hàng**: Giỏ hàng được lưu trữ bền vững tại
  Database thay vì bộ nhớ tạm trình duyệt, cho phép khách hàng đồng bộ
  trải nghiệm trên mọi thiết bị. Mọi đơn hàng (Orders) đều được chốt
  cứng giá tại thời điểm giao dịch, bảo vệ lịch sử hóa đơn trước mọi
  biến động giá thị trường.

**IV. CÁC MODULE NGHIỆP VỤ**

**1. Phân hệ Quản lý Kho thông minh**

Hệ thống thực hiện quy trình kiểm tra động liên tục: ngay khi khách hàng
tương tác, hệ thống sẽ tự động thực hiện \"truy vấn ngầm\" để xác minh
tồn kho thực tế, đảm bảo giao diện luôn phản ánh chính xác khả năng cung
ứng.

**2. Quy trình \"Sacred Checkout\" (Thanh toán an toàn)**

Đây là trái tim của hệ thống, nơi áp dụng tư duy \"Booking\" để bảo vệ
đơn hàng qua cơ chế Xác minh đa tầng: Backend sẽ tự mình tính toán lại
toàn bộ hóa đơn từ dữ liệu gốc, không tin tưởng vào bất kỳ thông số nào
gửi từ Frontend. Toàn bộ chuỗi thao tác: **Kiểm tra kho -\> Tạo đơn -\>
Trừ kho -\> Xóa giỏ hàng** được gói gọn trong một Transaction duy nhất.
Nếu một mắt xích lỗi, toàn bộ sẽ Rollback để đảm bảo an toàn.

**3. Vòng đời đơn hàng & Hoàn tồn (Restock)**

Đơn hàng đi qua các trạng thái từ **PENDING đến COMPLETED** một cách
minh bạch. Đặc biệt, nếu một **đơn hàng bị hủy**, quy trình **Restock sẽ
tự động kích hoạt,** cộng trả số lượng hàng về kho để tiếp tục phục vụ
khách hàng khác, tối ưu hóa dòng vòng quay hàng hóa.

V. LỘ TRÌNH 7 NGÀY \"CHẠY NƯỚC RÚT\" (1-WEEK BLITZ)

Với quỹ thời gian hạn hẹp, chúng ta sẽ tập trung vào các mắt xích sống
còn:

- Ngày 1: Cập nhật Database (thêm RowVersion và chuẩn hóa Status). Ánh
  xạ Entities vào dự án BaseCore.

- Ngày 2: Triển khai API Sản phẩm và dùng Axios đổ dữ liệu thật lên
  trang Shop của Kaira.

- Ngày 3: Xây dựng logic Giỏ hàng lưu trữ trực tiếp vào SQL Server.

- Ngày 4: Viết OrderService -- Triển khai logic trừ tồn kho và
  Transaction (Phần quan trọng nhất).

- Ngày 5: Hoàn thiện giao diện Checkout và tích hợp bảo mật qua JWT
  Token.

- Ngày 6: Thiết lập Dashboard Admin đơn giản để thực hiện các thao tác
  Duyệt/Hủy đơn hàng.

- Ngày 7: Kiểm thử tổng thể: Test tranh chấp đồng thời (Concurrency) và
  đóng gói sản phẩm.

**XÂY DỰNG HỆ THỐNG MỸ PHẨM TRỰC TUYẾN\
GLOWHUB**

![](media/image1.png){width="6.299305555555556in"
height="4.885416666666667in"}

**Tổng quan tài nguyên sẵn có**

Bạn đã có: backend **BaseCore** (ApiGateway, APIService, AuthService,
Repository, Services, Entities, DTO, WebClient), database **script.sql**
(bảng Categories, Products, Users, Orders, CartItems\...) và frontend
**Kaira template** (Vite + React + App.jsx, style.css, index.html). Mục
tiêu là nối tất cả lại thành hệ thống GlowHub hoàn chỉnh.

**Ngày 1 --- Chuẩn bị database và ánh xạ Entities**

**Việc cần làm trên SQL Server:** Chạy script.sql để tạo BaseCoreDB. Sau
đó bổ sung 2 điều mà đặc tả yêu cầu nhưng script gốc có thể chưa có:
thêm cột RowVersion TIMESTAMP vào bảng Products (phục vụ Optimistic
Concurrency), và chuẩn hóa cột Status trong bảng Orders với các giá trị
PENDING, CONFIRMED, COMPLETED, CANCELLED.

**Việc cần làm trong BaseCore.Entities:** Tạo các class C# tương ứng:
Category, Product (có thuộc tính \[Timestamp\] public byte\[\]
RowVersion), User, Order, OrderDetail, CartItem. Sau đó mở DbContext
trong BaseCore.Repository và đăng ký tất cả DbSet\<\> + cấu hình
FluentAPI cho quan hệ khóa ngoại.

**Ngày 2 --- API sản phẩm + đổ dữ liệu lên Kaira Shop**

**Việc cần làm trong BaseCore.APIService:** Tạo ProductsController với 3
endpoint: GET /api/products (danh sách + lọc theo category), GET
/api/products/{id} (chi tiết), GET /api/categories (danh mục). Trong
BaseCore.Services, viết ProductService chứa logic truy vấn qua
Repository.

**Việc cần làm trong BaseCore.WebClient (Kaira + React):** Cài axios.
Tạo file src/services/api.js đặt baseURL trỏ vào port của APIService.
Tạo component ProductCard.jsx rồi thay thế các sản phẩm tĩnh trong
template Kaira bằng useEffect + axios.get(\'/api/products\'). Trang Shop
sẽ hiển thị dữ liệu thật từ SQL Server.

**Ngày 3 --- Giỏ hàng lưu vào SQL Server**

Đặc tả yêu cầu giỏ hàng phải lưu DB thay vì localStorage để đồng bộ đa
thiết bị.

**Backend:** Tạo CartController với các endpoint GET /cart, POST
/cart/add, DELETE /cart/remove/{itemId}, PUT /cart/update. Mỗi action
đều lấy userId từ JWT để biết giỏ của ai.

**Frontend:** Tạo CartContext hoặc Zustand store. Mỗi khi user nhấn
\"Thêm vào giỏ\", gọi POST /api/cart/add thay vì cập nhật state local.
Trang Cart render bằng cách GET /api/cart khi component mount.

**Ngày 4 --- OrderService và Transaction (quan trọng nhất)**

Đây là \"trái tim\" của hệ thống theo đúng đặc tả --- logic Sacred
Checkout.

**Trong BaseCore.Services/OrderService.cs**, toàn bộ quy trình checkout
phải nằm trong một using var transaction = await
\_context.Database.BeginTransactionAsync():

1.  Lấy CartItems của user từ DB

2.  Backend tự tính lại tổng tiền từ giá trong DB (không tin frontend)

3.  Kiểm tra StockQuantity từng sản phẩm --- nếu thiếu hàng thì throw
    exception

4.  Trừ kho: product.StockQuantity -= quantity với kiểm tra RowVersion
    (Optimistic Concurrency --- nếu bị conflict do user khác mua đồng
    thời thì rollback)

5.  Tạo bản ghi Order + OrderDetail với giá chốt tại thời điểm

6.  Xóa CartItems

7.  await transaction.CommitAsync()

Nếu bất kỳ bước nào lỗi, catch sẽ await transaction.RollbackAsync() ---
không có trạng thái dở dang.

**Cũng ở Ngày 4:** Viết logic Restock --- khi Admin hủy đơn (CANCELLED),
OrderService.CancelOrder() cộng lại số lượng vào StockQuantity.

**Ngày 5 --- Giao diện Checkout và bảo mật JWT**

**Backend (BaseCore.AuthService):** Đã có sẵn, chỉ cần kiểm tra endpoint
POST /api/auth/login và POST /api/auth/register hoạt động đúng, trả về
JWT token.

**Frontend:** Tạo trang Checkout.jsx lấy thông tin địa chỉ giao hàng.
Khi submit, gọi POST /api/orders/checkout kèm header Authorization:
Bearer \<token\>. Lưu JWT vào localStorage (hoặc cookie httpOnly tốt
hơn). Tạo axios interceptor để tự gắn token vào mọi request cần auth.

Bảo vệ các route nhạy cảm trên React bằng PrivateRoute --- nếu chưa đăng
nhập thì redirect về trang Login.

**Ngày 6 --- Dashboard Admin đơn giản**

**Backend:** Thêm \[Authorize(Roles = \"Admin\")\] vào AdminController
với các endpoint: GET /admin/orders (danh sách đơn theo status), PUT
/admin/orders/{id}/confirm, PUT /admin/orders/{id}/cancel.

**Frontend:** Tạo route /admin với layout riêng. Hiển thị bảng đơn hàng
với các nút Duyệt / Hủy. Khi nhấn Hủy thì backend tự kích hoạt Restock.
Admin cũng cần xem được tồn kho sản phẩm và cập nhật.

**Ngày 7 --- Kiểm thử tổng thể**

Tập trung vào 3 kịch bản chính: (1) **Concurrency test** --- mô phỏng 2
user mua cùng 1 sản phẩm còn 1 cái cùng lúc, chỉ 1 người được thành
công; (2) **Rollback test** --- tắt DB giữa chừng khi đang checkout,
kiểm tra dữ liệu không bị nửa vời; (3) **Restock test** --- hủy đơn rồi
kiểm tra kho cộng lại đúng. Sau đó cấu hình CORS trong ApiGateway cho
đúng origin của frontend và chạy npm run build để đóng gói production.

**Điểm cần lưu ý đặc biệt**

Kaira là HTML template thuần --- bạn đang dùng nó trong môi trường
Vite + React nên cần chuyển các file tĩnh trong thư mục
public/kaira-1.0.0/ thành import đúng cách trong React (CSS vào
index.css, ảnh vào src/assets). Các component HTML của Kaira cần được
viết lại thành JSX. Nên bắt đầu từ trang Shop và ProductDetail trước, vì
đó là luồng người dùng quan trọng nhất.
