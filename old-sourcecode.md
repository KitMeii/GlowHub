# Tài liệu mô tả cấu trúc source code

# Foder: Common

## 1. Giới thiệu

Project BaseCore.Common là thư viện chứa các thành phần dùng chung cho
toàn bộ hệ thống trong solution BaseCore. Các lớp trong project này cung
cấp các chức năng hỗ trợ như xác thực người dùng, xử lý file, quản lý
WebSocket, các lớp tiện ích và các cấu hình hệ thống.

Việc tách các chức năng dùng chung vào project Common giúp:

- Tăng khả năng tái sử dụng code

- Giảm trùng lặp code giữa các project

- Giúp hệ thống dễ bảo trì và mở rộng

Trong project BaseCore.Common, source code được tổ chức thành các thư
mục chính sau:

- Auth

- Extensions

- Helpers

- Sockets

Ngoài ra còn có một số file cấu hình và lớp dùng chung của hệ thống.

## 2. Thư mục Auth

**Chức năng:**

Thư mục Auth chứa các lớp liên quan đến xác thực (Authentication) và
phân quyền (Authorization) của hệ thống.

Thư mục này hỗ trợ việc quản lý quyền truy cập của người dùng và xử lý
JWT Token trong quá trình đăng nhập và xác thực API.

**Thành phần trong thư mục**

**RoleConstant.cs**

File này định nghĩa các vai trò (Role) của người dùng trong hệ thống.

Ví dụ:

- Admin

- User

- Manager

Mục đích của file này là quản lý tập trung các role của hệ thống, tránh
việc khai báo chuỗi role trực tiếp ở nhiều nơi trong code.

TokenHelper.cs

File này chứa các phương thức hỗ trợ tạo và xử lý JWT Token.

Các chức năng chính bao gồm:

- Tạo token khi người dùng đăng nhập

- Kiểm tra token hợp lệ

- Lấy thông tin người dùng từ token

Lớp này đóng vai trò quan trọng trong việc thực hiện cơ chế xác thực
người dùng thông qua token trong hệ thống API.

## 3. Thư mục Extensions

**Chức năng:**

Thư mục Extensions chứa các extension methods, giúp mở rộng chức năng
của các lớp có sẵn trong .NET mà không cần thay đổi mã nguồn gốc của lớp
đó.

Các extension giúp code ngắn gọn hơn và dễ tái sử dụng trong nhiều nơi
của hệ thống.

**Thành phần trong thư mục**

**MediaExtension.cs**

File này chứa các phương thức mở rộng liên quan đến xử lý file media.

Các chức năng có thể bao gồm:

- Kiểm tra định dạng file

- Xác định loại file

- Hỗ trợ xử lý file upload

Mục đích của lớp này là hỗ trợ việc làm việc với các file media trong hệ
thống.

## 4. Thư mục Helpers

**Chức năng:**

Thư mục Helpers chứa các lớp tiện ích hỗ trợ xử lý các tác vụ chung
trong hệ thống.

Các helper giúp tách các chức năng hỗ trợ ra khỏi các module chính, từ
đó giúp code rõ ràng và dễ quản lý hơn.

**Thành phần trong thư mục**

**EnumHelper.cs**

Lớp này hỗ trợ các thao tác liên quan đến Enum.

Các chức năng phổ biến:

- Lấy danh sách các giá trị của enum

- Chuyển enum sang chuỗi

- Chuyển chuỗi sang enum

Mục đích là giúp việc xử lý dữ liệu dạng enum trở nên thuận tiện hơn.

FileHelper.cs

Lớp này hỗ trợ các thao tác liên quan đến xử lý file trong hệ thống.

Ví dụ các chức năng:

- Upload file

- Lưu file vào server

- Kiểm tra kích thước file

- Đọc nội dung file

Việc tập trung các chức năng xử lý file trong một lớp helper giúp tăng
khả năng tái sử dụng và giảm sự trùng lặp code.

## 5. Thư mục Sockets

**Chức năng:**

Thư mục Sockets chứa các lớp hỗ trợ WebSocket, cho phép hệ thống thực
hiện giao tiếp thời gian thực (real-time communication) giữa server và
client.

WebSocket thường được sử dụng trong các chức năng như:

- Chat realtime

- Thông báo realtime

- Cập nhật dữ liệu trực tiếp

**Thành phần trong thư mục**

**WebSocketConnectionManager.cs**

Lớp này dùng để quản lý danh sách các kết nối WebSocket đang hoạt động.

Chức năng chính:

- Lưu danh sách client đang kết nối

- Thêm hoặc xóa kết nối WebSocket

- WebSocketHandler.cs

Lớp này xử lý các sự kiện gửi và nhận dữ liệu giữa client và server
thông qua WebSocket.

Các chức năng có thể bao gồm:

- Nhận message từ client

- Gửi message tới client

- Gửi message tới nhiều client cùng lúc

**WebSocketManagerExtensions.cs**

File này chứa các extension methods giúp cấu hình WebSocket trong ứng
dụng ASP.NET Core một cách thuận tiện.

**WebSocketManagerMiddleware.cs**

Middleware này được sử dụng để xử lý request WebSocket trong pipeline
của ứng dụng.

Nó đóng vai trò trung gian giúp server tiếp nhận và xử lý các kết nối
WebSocket từ client.

## 6. Các file dùng chung trong project

Ngoài các thư mục trên, project còn chứa một số file dùng chung cho toàn
hệ thống.

**AppSettings.cs**

File này dùng để ánh xạ các cấu hình trong appsettings.json vào code.

Ví dụ các cấu hình:

- JWT Configuration

- Redis Configuration

- Các cấu hình hệ thống khác

Lớp này giúp truy cập các cấu hình hệ thống dễ dàng hơn trong chương
trình.

**Constants.cs**

File này chứa các hằng số dùng chung trong toàn hệ thống.

Ví dụ:

- Tên key

- Các thông báo lỗi

- Các giá trị cấu hình cố định

Việc sử dụng constants giúp tránh việc hardcode các giá trị trực tiếp
trong code.

**Entity.cs**

Đây là lớp entity cơ sở (Base Entity).

Lớp này chứa các thuộc tính chung cho các entity khác trong hệ thống.

Ví dụ:

- Id

- CreatedDate

- UpdatedDate

- Status

Các entity khác trong hệ thống có thể kế thừa từ lớp này để sử dụng lại
các thuộc tính chung.

**Enums.cs**

File này định nghĩa các kiểu dữ liệu Enum được sử dụng trong hệ thống.

Ví dụ:

- trạng thái đơn hàng

- trạng thái người dùng

- loại dữ liệu

Việc sử dụng enum giúp code rõ ràng và dễ quản lý hơn.

**RedisUtils.cs**

File này chứa các phương thức hỗ trợ làm việc với Redis Cache.

Các chức năng có thể bao gồm:

- Lưu dữ liệu vào Redis

- Lấy dữ liệu từ Redis

- Xóa dữ liệu cache

Redis được sử dụng để tăng tốc độ truy xuất dữ liệu và giảm tải cho
database.

# Foder: DataAccess

## 1. Giới thiệu

Thư mục DataAccess trong hệ thống BaseCore chịu trách nhiệm quản lý toàn
bộ các thành phần liên quan đến truy cập và xử lý dữ liệu. Đây là tầng
làm việc trực tiếp với cơ sở dữ liệu của hệ thống.

Module này được tổ chức thành các project chính:

- BaseCore.Entities

- BaseCore.Libs

- BaseCore.Repository

Mỗi project có một vai trò riêng nhằm tách biệt rõ ràng giữa định nghĩa
dữ liệu, các thư viện hỗ trợ và các lớp truy cập dữ liệu.

## 2. Project BaseCore.Entities

**Chức năng:**

Project BaseCore.Entities chứa các lớp Entity (Model dữ liệu) của hệ
thống.

Entity là các lớp đại diện cho bảng dữ liệu trong cơ sở dữ liệu. Các lớp
này được sử dụng để ánh xạ dữ liệu giữa ứng dụng và database thông qua
ORM như Entity Framework.

**Thành phần trong project**

**Folder Audit**

Thư mục Audit dùng để lưu các lớp liên quan đến theo dõi lịch sử thay
đổi dữ liệu trong hệ thống.

Ví dụ:

- Lưu thông tin ai đã tạo dữ liệu

- Thời gian tạo hoặc cập nhật dữ liệu

- Lịch sử thay đổi dữ liệu

Mục đích của audit là giúp hệ thống có thể kiểm soát và theo dõi các
thay đổi dữ liệu.

**Các lớp Entity chính**

Dưới đây là các lớp đại diện cho các bảng dữ liệu của hệ thống.

**AccessToken.cs**

Entity lưu thông tin token truy cập của người dùng.

Ví dụ thông tin lưu trữ:

- Token

- UserId

- Thời gian hết hạn

Mục đích: phục vụ cho cơ chế xác thực người dùng.

**Category.cs**

Entity đại diện cho danh mục sản phẩm.

Ví dụ dữ liệu:

- Id

- Name

- Description

Mục đích: phân loại các sản phẩm trong hệ thống.

**Function.cs**

Entity lưu thông tin về các chức năng của hệ thống.

Ví dụ:

- Quản lý sản phẩm

- Quản lý người dùng

- Quản lý đơn hàng

Entity này thường được sử dụng cho phân quyền hệ thống.

**GroupRoleAgency.cs**

Entity dùng để quản lý nhóm quyền của người dùng theo từng tổ chức hoặc
đơn vị.

Mục đích: hỗ trợ hệ thống phân quyền phức tạp theo nhóm.

**Module.cs**

Entity đại diện cho module chức năng của hệ thống.

Ví dụ module:

- User Management

- Product Management

- Order Management

  **ModuleFunction.cs**

Entity dùng để liên kết giữa Module và Function.

Mục đích:

Xác định chức năng nào thuộc module nào.

**Order.cs**

Entity lưu thông tin đơn hàng.

Ví dụ:

- OrderId

- CustomerId

- OrderDate

- TotalAmount

  **OrderDetail.cs**

Entity lưu thông tin chi tiết của từng sản phẩm trong đơn hàng.

Ví dụ:

- OrderId

- ProductId

- Quantity

- Price

  **Product.cs**

Entity lưu thông tin sản phẩm.

Ví dụ:

- ProductId

- ProductName

- Price

- CategoryId

  **Role.cs**

Entity lưu thông tin vai trò của người dùng trong hệ thống.

Ví dụ:

- Admin

- User

- Manager

**RoleModuleFunction.cs**

Entity dùng để liên kết Role với Module và Function.

Mục đích:

Xác định role nào được phép truy cập chức năng nào.

**SeedConfiguration.cs**

File này dùng để khởi tạo dữ liệu mặc định cho hệ thống.

Ví dụ:

- Tạo role mặc định

- Tạo user admin ban đầu

**Setting.cs**

Entity lưu các cấu hình hệ thống.

Ví dụ:

- cấu hình email

- cấu hình hệ thống

**User.cs**

Entity đại diện cho người dùng của hệ thống.

Ví dụ:

- UserId

- Username

- Password

- Email

**UserModule.cs**

Entity dùng để liên kết người dùng với module hệ thống.

Mục đích: quản lý quyền truy cập module.

**UserRole.cs**

Entity dùng để liên kết người dùng với role.

Một user có thể có nhiều role.

## 3. Project BaseCore.Libs

**Chức năng:**

Project BaseCore.Libs chứa các thư viện tiện ích hỗ trợ xử lý dữ liệu và
logic chung cho hệ thống.

Các lớp trong project này thường được sử dụng bởi nhiều module khác
nhau.

**Thành phần trong project**

**Folder Utils**

Thư mục Utils chứa các lớp tiện ích hỗ trợ xử lý dữ liệu.

**EnumHelper.cs**

Lớp hỗ trợ các thao tác với Enum.

Ví dụ:

- Chuyển enum sang string

- Lấy danh sách giá trị enum

**LinqExtension.cs**

File này chứa các extension methods cho LINQ.

Ví dụ:

- Hỗ trợ truy vấn dữ liệu

- Mở rộng các chức năng của LINQ

**Utils.cs**

Lớp chứa các hàm tiện ích chung cho hệ thống.

Ví dụ:

- xử lý chuỗi

- chuyển đổi dữ liệu

- kiểm tra dữ liệu

## 4. Project BaseCore.Repository

**Chức năng:**

Project BaseCore.Repository chứa các lớp Repository, chịu trách nhiệm
truy cập dữ liệu từ database.

Repository là tầng trung gian giữa Business Logic (Service) và Database.

Nó giúp tách biệt logic truy cập dữ liệu khỏi logic nghiệp vụ.

**Thành phần trong project**

**Folder Authen**

Thư mục này chứa các repository liên quan đến xác thực người dùng.

**UserRepository.cs**

Lớp này thực hiện các thao tác truy vấn dữ liệu người dùng từ database.

Ví dụ:

- Tìm user theo username

- Kiểm tra đăng nhập

- Folder EFCore

Thư mục này chứa các repository làm việc với Entity Framework Core.

**IRepository.cs**

Interface định nghĩa các phương thức cơ bản cho repository.

Ví dụ:

- GetAll()

- GetById()

- Add()

- Update()

- Delete()

**Repository.cs**

Lớp cài đặt chung cho các repository.

Các repository khác có thể kế thừa lớp này để sử dụng lại các phương
thức CRUD.

**CategoryRepository.cs**

Repository xử lý dữ liệu Category.

**ProductRepository.cs**

Repository xử lý dữ liệu Product.

**OrderRepository.cs**

Repository xử lý dữ liệu Order.

**UserRepository.cs**

Repository xử lý dữ liệu User.

## 5. Folder Migrations

Thư mục Migrations chứa các file migration của Entity Framework.

Migration giúp:

- Tạo bảng trong database

- Cập nhật cấu trúc database

- Quản lý version của database

## 6. Các file cấu hình Database

**MongoDbContext.cs**

Lớp cấu hình kết nối và làm việc với MongoDB.

MongoDB thường được sử dụng để lưu các dữ liệu dạng NoSQL.

**MySqlDbContext.cs**

Lớp cấu hình kết nối với MySQL Database thông qua Entity Framework.

DbContext chịu trách nhiệm:

- ánh xạ entity với bảng database

- quản lý truy vấn dữ liệu

# Foder: Microservices

## 1. Giới thiệu

Thư mục Microservices trong hệ thống BaseCore chứa các dịch vụ được xây
dựng theo kiến trúc Microservice. Mỗi service trong thư mục này đảm
nhiệm một chức năng riêng của hệ thống và có thể hoạt động độc lập.

Kiến trúc Microservices giúp hệ thống:

- Tách các chức năng thành các dịch vụ nhỏ độc lập

- Dễ mở rộng và triển khai

- Giảm sự phụ thuộc giữa các module

- Cho phép nhiều nhóm phát triển song song

Trong module này bao gồm các project chính:

- BaseCore.ApiGateway

- BaseCore.APIService

- BaseCore.AuthService

Mỗi project đảm nhiệm một vai trò khác nhau trong hệ thống.

## 2. Project BaseCore.ApiGateway

**Chức năng:**

Project BaseCore.ApiGateway đóng vai trò là cổng trung gian (API
Gateway) của hệ thống.

API Gateway là điểm tiếp nhận tất cả các request từ client, sau đó
chuyển tiếp các request này đến các microservice tương ứng.

**Vai trò của API Gateway**

- Tiếp nhận request từ client

- Chuyển tiếp request đến service phù hợp

- Tổng hợp dữ liệu từ nhiều service nếu cần

- Quản lý bảo mật và xác thực request

**Thành phần trong project**

**appsettings.json**

File này chứa các cấu hình của API Gateway, bao gồm các thông tin cấu
hình cho hệ thống và routing của gateway.

**ocelot.json**

File cấu hình của Ocelot API Gateway.

Ocelot là thư viện trong .NET dùng để xây dựng API Gateway.

File này định nghĩa:

- Route của API

- Service nào sẽ xử lý request

- Cổng kết nối giữa gateway và microservice

**Program.cs**

File khởi tạo và cấu hình ứng dụng.

Trong file này hệ thống sẽ:

- cấu hình API Gateway

- đăng ký middleware

- khởi động ứng dụng

## 3. Project BaseCore.APIService

**Chức năng:**

Project BaseCore.APIService là service chính của hệ thống, cung cấp các
RESTful API phục vụ cho các chức năng nghiệp vụ như quản lý sản phẩm,
danh mục, đơn hàng,\...

Service này chịu trách nhiệm xử lý logic liên quan đến dữ liệu và trả
kết quả cho client.

**Thành phần trong project**

**Controllers**

Thư mục Controllers chứa các lớp Controller để xử lý request từ client.

Controller nhận request từ client và gọi các service hoặc repository để
xử lý dữ liệu.

Ví dụ:

- ProductController

- CategoryController

- OrderController

**appsettings.json**

File cấu hình của service.

Các thông tin cấu hình có thể bao gồm:

- connection string

- cấu hình hệ thống

- cấu hình logging

**Program.cs**

File khởi động ứng dụng ASP.NET Core.

Nó thực hiện:

- cấu hình service

- đăng ký middleware

- khởi chạy API

## 4. Project BaseCore.AuthService

**Chức năng:**

Project BaseCore.AuthService chịu trách nhiệm quản lý xác thực và phân
quyền người dùng trong hệ thống.

Service này xử lý các chức năng như:

- đăng nhập

- quản lý người dùng

- quản lý role

- cấp phát token

## 5. Cấu trúc Frontend trong AuthService

Project AuthService tích hợp cả frontend và backend để phục vụ giao diện
quản trị hệ thống.

Frontend được xây dựng bằng ReactJS.

**Folder ClientApp**

Thư mục ClientApp chứa toàn bộ mã nguồn của ứng dụng frontend.

public

Thư mục này chứa các file tĩnh của ứng dụng frontend.

Ví dụ:

- file HTML gốc

- favicon

- các tài nguyên tĩnh

**src**

Thư mục src chứa mã nguồn chính của ứng dụng React.

**components**

Thư mục này chứa các component giao diện có thể tái sử dụng.

Ví dụ:

- form

- bảng dữ liệu

- thanh điều hướng

  **contexts**

Thư mục này chứa các React Context để quản lý trạng thái toàn cục của
ứng dụng.

Ví dụ:

**AuthContext.jsx**

Context dùng để quản lý thông tin xác thực của người dùng như:

- trạng thái đăng nhập

- token

- thông tin user

  **pages**

Thư mục này chứa các trang chính của ứng dụng.

Các trang tiêu biểu gồm:

- Login.jsx: trang đăng nhập hệ thống

- Dashboard.jsx: trang quản trị tổng quan

- Products.jsx: quản lý sản phẩm

- Categories.jsx: quản lý danh mục

- Users.jsx: quản lý người dùng

**services**

Thư mục này chứa các lớp dùng để gọi API từ frontend đến backend.

Ví dụ:

- gọi API đăng nhập

- gọi API lấy danh sách sản phẩm

- gọi API quản lý người dùng

**App.jsx**

File chính của ứng dụng React.

File này định nghĩa:

- routing của ứng dụng

- layout tổng thể

**index.js**

File entry point của ứng dụng React.

File này dùng để render ứng dụng React vào trang HTML.

**package.json**

File cấu hình của dự án React.

File này quản lý:

- danh sách thư viện sử dụng

- script chạy ứng dụng

- dependency của project

## 6. Backend trong AuthService

Ngoài phần frontend, project AuthService còn chứa backend ASP.NET Core.

**Controllers**

Thư mục này chứa các controller xử lý các chức năng liên quan đến xác
thực và quản lý người dùng.

Các controller bao gồm:

**AuthController.cs**

Xử lý các chức năng liên quan đến đăng nhập và xác thực người dùng.

Ví dụ:

- đăng nhập

- tạo token

**RolesController.cs**

Controller dùng để quản lý vai trò người dùng.

Ví dụ:

- tạo role

- cập nhật role

- xóa role

**UserController.cs**

Controller xử lý các chức năng liên quan đến quản lý người dùng.

Ví dụ:

- tạo user

- cập nhật user

- lấy danh sách user

**Models**

Thư mục chứa các model dữ liệu sử dụng trong controller hoặc view.

Ví dụ:

**ErrorViewModel.cs**

Model dùng để hiển thị thông tin lỗi trong hệ thống.

**Views**

Thư mục này chứa các view của ASP.NET MVC.

- Các view được tổ chức thành:

- Home: giao diện trang chủ

- Shared: các view dùng chung

Ngoài ra còn có các file:

- \_ViewImports.cshtml: cấu hình import cho view

- \_ViewStart.cshtml: cấu hình layout mặc định

**appsettings.json**

File cấu hình của AuthService.

Chứa các thông tin như:

- cấu hình hệ thống

- cấu hình database

- cấu hình JWT

**Program.cs**

File khởi động của ứng dụng ASP.NET Core.

Nhiệm vụ của file này là:

- cấu hình service

- đăng ký middleware

- khởi chạy ứng dụng

# Foder: Services

## 1. Giới thiệu

Thư mục Services trong hệ thống BaseCore chứa các thành phần thực hiện
logic nghiệp vụ (Business Logic) của hệ thống.

Tầng Services đóng vai trò trung gian giữa Controller và tầng
DataAccess, giúp xử lý các quy tắc nghiệp vụ trước khi dữ liệu được lưu
vào database hoặc trả về cho client.

Việc tách tầng Services giúp:

- Tách biệt logic nghiệp vụ khỏi Controller

- Dễ bảo trì và mở rộng hệ thống

- Dễ kiểm thử (Unit Test)

- Tăng tính tái sử dụng của code

Trong module này bao gồm hai project chính:

- BaseCore.DTO

- BaseCore.Services

## 2. Project BaseCore.DTO

**Chức năng:**

Project BaseCore.DTO chứa các lớp DTO (Data Transfer Object) được sử
dụng để truyền dữ liệu giữa các tầng của hệ thống như:

- Controller

- Service

- Repository

- API

DTO giúp tách biệt dữ liệu truyền đi với Entity của database, từ đó tăng
tính bảo mật và kiểm soát dữ liệu tốt hơn.

**Các thư mục trong project**

**AuthPlatform**

Thư mục này chứa các DTO liên quan đến xác thực và phân quyền người
dùng.

Ví dụ các DTO:

- LoginRequest

- LoginResponse

- RegisterRequest

Mục đích:

- Truyền dữ liệu đăng nhập giữa client và server.

**Common**

Thư mục này chứa các DTO dùng chung trong toàn hệ thống.

Ví dụ:

- PagingRequest

- PagingResponse

- BaseResponse

Mục đích:

Chuẩn hóa dữ liệu truyền giữa các tầng.

**Response**

Thư mục này chứa các DTO dùng để trả dữ liệu từ API về client.

Ví dụ:

- ApiResponse

- ErrorResponse

Mục đích:

Chuẩn hóa định dạng dữ liệu phản hồi từ hệ thống.

**Robot**

Thư mục này chứa các DTO liên quan đến các chức năng tự động hoặc tích
hợp hệ thống (ví dụ tích hợp bot hoặc automation).

Các DTO trong thư mục này được sử dụng để truyền dữ liệu giữa hệ thống
và các module tự động.

## 3. Project BaseCore.Services

**Chức năng:**

Project BaseCore.Services chứa các lớp thực hiện logic nghiệp vụ của hệ
thống.

Các service sẽ:

- Nhận dữ liệu từ Controller

- Xử lý logic nghiệp vụ

- Gọi Repository để truy cập dữ liệu

- Trả kết quả về Controller

## 4. Folder Authen

**Chức năng:**

Thư mục Authen chứa các service liên quan đến xác thực và phân quyền
người dùng.

Các service trong thư mục này xử lý các chức năng như:

- đăng nhập

- xác thực token

- kiểm tra quyền truy cập

## 5. Các Service chính

**CategoryService.cs**

Service này xử lý các nghiệp vụ liên quan đến danh mục sản phẩm
(Category).

Ví dụ chức năng:

- Lấy danh sách danh mục

- Tạo danh mục mới

- Cập nhật danh mục

- Xóa danh mục

**ICategoryService.cs**

Đây là interface định nghĩa các phương thức của CategoryService.

Ví dụ:

- GetAllCategories()

- CreateCategory()

- UpdateCategory()

- DeleteCategory()

Việc sử dụng interface giúp:

- Tách biệt định nghĩa và triển khai

- Hỗ trợ Dependency Injection

- Dễ viết Unit Test

**IOrderService.cs**

Interface định nghĩa các phương thức xử lý nghiệp vụ đơn hàng.

Ví dụ:

- CreateOrder()

- GetOrderById()

- GetAllOrders()

**IProductService.cs**

Interface định nghĩa các chức năng liên quan đến sản phẩm.

Ví dụ:

- GetProducts()

- CreateProduct()

- UpdateProduct()

- DeleteProduct()

**OrderService.cs**

Lớp triển khai các phương thức trong IOrderService.

Service này xử lý các logic liên quan đến:

- tạo đơn hàng

- tính tổng tiền

- quản lý thông tin đơn hàng

**ProductService.cs**

Lớp triển khai các chức năng trong IProductService.

Service này xử lý các logic liên quan đến:

- quản lý sản phẩm

- cập nhật thông tin sản phẩm

- truy vấn danh sách sản phẩm

## 6. Mối quan hệ giữa các tầng

Trong kiến trúc của hệ thống BaseCore, tầng Services hoạt động theo
luồng sau:

**Client Request**

- Controller

- Service (xử lý logic nghiệp vụ)

- Repository (truy cập dữ liệu)

- Database

Sau khi xử lý xong:

**Database**

- Repository

- Service

- Controller

- Client
