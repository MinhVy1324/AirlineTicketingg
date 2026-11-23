using System.Globalization;
using AirlineTicketing.BUS;
using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Infrastructure;

public static class BookingUiText
{
    private static readonly IReadOnlyDictionary<string, string> EnglishLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Một chiều"] = "One way",
            ["Khứ hồi"] = "Round trip",
            ["Chiều đi"] = "Outbound",
            ["Chiều về"] = "Return",
            ["Bay thẳng"] = "Nonstop",
            ["Xách tay 12 kg"] = "12 kg carry-on",
            ["Ký gửi 23 kg"] = "23 kg checked baggage",
            ["Không mua thêm"] = "No extra baggage",
            ["Không có hành lý riêng"] = "No separate baggage allowance",
            ["Ghế tiêu chuẩn"] = "Standard seat",
            ["Ghế rộng chân"] = "Extra-legroom seat",
            ["Ghế phổ thông đặc biệt"] = "Preferred seat",
            ["Chưa chọn chỗ"] = "No seat selected",
            ["Cạnh cửa sổ"] = "Window",
            ["Cạnh lối đi"] = "Aisle",
            ["Ở giữa"] = "Middle seat",
            ["Miễn phí"] = "Free",
            ["Tự động"] = "Assigned automatically",
            ["Chọn chuyến"] = "Select flight",
            ["Chọn chiều đi"] = "Select outbound flight",
            ["Chọn chiều về"] = "Select return flight",
            ["Đề xuất"] = "Recommended",
            ["Giá thấp nhất"] = "Lowest fare",
            ["Giờ bay sớm nhất"] = "Earliest departure",
            ["Bay nhanh nhất"] = "Shortest duration",
            ["Đêm / sáng sớm · 00:00–04:59"] = "Night / early morning · 00:00–04:59",
            ["Sáng · 05:00–11:59"] = "Morning · 05:00–11:59",
            ["Chiều · 12:00–17:59"] = "Afternoon · 12:00–17:59",
            ["Tối · 18:00–23:59"] = "Evening · 18:00–23:59",
            ["Chi tiết thanh toán"] = "Payment breakdown",
            ["Tổng tiền thanh toán"] = "Payment total",
            ["Tổng thanh toán"] = "Total due",
            ["Tổng cộng"] = "Total",
            ["Giá vé cơ bản"] = "Base fare",
            ["Hành lý thêm 10 kg"] = "Extra 10 kg baggage",
            ["Hành lý ký gửi bổ sung"] = "Extra checked baggage",
            ["Chọn chỗ ngồi"] = "Seat selection",
            ["Chỗ ngồi · chưa chọn"] = "Seat · not selected",
            ["Giá đã bao gồm thuế, phí · Thanh toán bằng VND"] = "Taxes and fees included · Payment in VND",
            ["Giá vé cả nhóm / tất cả chặng"] = "Group fare for all flights",
            ["Phụ thu ghế cả nhóm"] = "Group seat fees",
            ["Hành lý mua thêm cả nhóm"] = "Extra baggage for the group",
            ["Thẻ ngân hàng"] = "Bank card",
            ["Chưa thanh toán"] = "Unpaid",
            ["Chuyển khoản ngân hàng · VietQR"] = "Bank transfer · VietQR",
            ["Trạng thái"] = "Status",
            ["Thông tin"] = "Details",
            ["Đang xử lý"] = "Processing",
            ["Diễn tiến đặt vé"] = "Booking history",
            ["Chuyến bay vừa hết chỗ"] = "The flight has just sold out",
            ["Chỗ bạn chọn đã được giữ bởi hành khách khác. Vui lòng chọn chuyến bay khác."] = "The seat you selected is held by another passenger. Choose another flight.",
            ["Thanh toán chưa thành công"] = "Payment unsuccessful",
            ["Đặt chỗ chưa được xác nhận. Bạn có thể thử lại hoặc chọn phương thức khác."] = "Your booking has not been confirmed. Retry or choose a different method.",
            ["Đặt chỗ đã hết hạn"] = "Booking expired",
            ["Đã hết thời gian giữ chỗ"] = "The booking hold has expired",
            ["Thời gian giữ chỗ đã kết thúc. Vui lòng tìm chuyến bay và đặt lại."] = "Your hold has expired. Search for flights again to rebook.",
            ["Đặt chỗ không còn chờ thanh toán"] = "The booking is no longer awaiting payment",
            ["Đặt chỗ này đã được xử lý hoặc không còn hiệu lực. Vui lòng kiểm tra Vé của tôi."] = "This booking has been processed or is no longer valid. Check My tickets.",
            ["Vui lòng tìm chuyến bay và đặt lại. Ghế đã được trả về sơ đồ."] = "Search for flights again to rebook. Your seats have been released.",
            ["Giao dịch chuyển khoản không còn hiệu lực. Vui lòng đặt lại chuyến bay."] = "The transfer is no longer valid. Book the flight again.",
            ["Yêu cầu của bạn đang được xử lý. Vui lòng kiểm tra lại trong Vé của tôi."] = "Your request is being processed. Check again in My tickets.",
            ["Vé điện tử đã được gửi qua kênh email của hệ thống."] = "Your e-ticket was sent by email.",
            ["Email vé đang chờ gửi. Bạn có thể tải PDF ngay."] = "Your ticket email is queued. You can download the PDF now.",
            ["Email chưa gửi được. Bạn có thể gửi lại hoặc tải PDF."] = "The email could not be sent. Resend it or download the PDF.",
            ["Đã bỏ mã ưu đãi."] = "Promotion code removed.",
            ["Đã áp dụng mã ưu đãi."] = "Promotion code applied.",
            ["Mã hãng hàng không không hợp lệ. Vui lòng chọn lại hãng."] = "Invalid airline code. Select an airline again.",
            ["Khung giờ không hợp lệ. Vui lòng chọn lại."] = "Invalid departure time period. Select another one.",
            ["Giá từ không được lớn hơn giá đến."] = "The minimum fare cannot exceed the maximum fare.",
            ["Nhập giá bằng số nguyên không âm, tối đa 9.999.999.999 ₫, không dùng dấu phân cách."] = "Enter a whole number between 0 and 9,999,999,999 VND, without separators.",
            ["Mỗi đơn có 1–9 hành khách, ít nhất một người lớn; số em bé không vượt số người lớn."] = "Each booking allows 1–9 passengers, including at least one adult and no more infants than adults.",
            ["Hạng vé không hợp lệ. Vui lòng chọn Phổ thông hoặc Thương gia."] = "Invalid cabin. Choose Economy or Business.",
            ["Vui lòng nhập họ, tối đa 70 ký tự."] = "Enter the surname using at most 70 characters.",
            ["Vui lòng nhập tên đệm và tên, tối đa 70 ký tự."] = "Enter the middle and given names using at most 70 characters.",
            ["Vui lòng chọn giới tính hợp lệ."] = "Select a valid gender.",
            ["Ngày sinh không hợp lệ. Nhập theo định dạng dd/MM/yyyy."] = "Enter a valid date of birth in dd/MM/yyyy format.",
            ["Ngày sinh không hợp lệ."] = "Invalid date of birth.",
            ["Người lớn phải từ 12 tuổi tại ngày bay."] = "Adults must be at least 12 years old on the flight date.",
            ["Trẻ em phải từ 2 đến dưới 12 tuổi tại ngày bay."] = "Children must be aged 2 to under 12 on the flight date.",
            ["Em bé phải từ 7 ngày đến dưới 2 tuổi tại ngày bay theo quy tắc demo."] = "Infants must be aged 7 days to under 2 years on the flight date under the demo rules.",
            ["Vui lòng nhập quốc tịch hợp lệ."] = "Enter a valid nationality.",
            ["Số giấy tờ phải có 6–30 chữ cái hoặc chữ số."] = "The ID number must contain 6–30 letters or digits.",
            ["Vui lòng nhập email liên hệ đúng định dạng."] = "Enter a valid contact email address.",
            ["Số điện thoại phải có 9–15 chữ số, có thể bắt đầu bằng +."] = "The phone number must contain 9–15 digits and may start with +.",
            ["Một chuyến bay không còn mở bán. Vui lòng chọn lại hành trình."] = "One of your flights is no longer on sale. Select another itinerary.",
            ["Chiều về phải ngược tuyến và khởi hành sau khi chiều đi hạ cánh."] = "The return flight must reverse the route and depart after the outbound flight arrives.",
            ["Vui lòng nhập tên người liên hệ, tối đa 150 ký tự."] = "Enter the contact name using at most 150 characters.",
            ["Email liên hệ không hợp lệ."] = "Enter a valid contact email address.",
            ["Số điện thoại liên hệ phải có 9–15 chữ số."] = "The contact phone number must contain 9–15 digits.",
            ["Số hồ sơ hành khách phải khớp với số khách đã chọn."] = "The number of passenger details must match the selected passenger count.",
            ["Giấy tờ bị lặp trong nhóm hành khách."] = "Two passengers in the group have the same ID document.",
            ["Có hai hồ sơ trùng họ tên và ngày sinh trong nhóm."] = "Two passengers in the group have the same name and date of birth.",
            ["Vui lòng chọn đủ ghế và dịch vụ cho từng chặng."] = "Select all required seats and services on each flight.",
            ["Gói hành lý không hợp lệ."] = "Invalid baggage option.",
            ["Gói hành lý không hợp lệ. Vui lòng chọn lại."] = "Invalid baggage option. Choose again.",
            ["Em bé đi cùng người lớn, không chọn ghế hay hành lý mua thêm riêng."] = "Infants travel on an adult's lap and have no separate seat or extra baggage.",
            ["Vui lòng chọn ghế cho hành khách."] = "Select a seat for this passenger.",
            ["Mỗi em bé cần một người lớn riêng từ 18 tuổi đi cùng ở cả hành trình."] = "Each infant must travel with a different adult aged 18 or over throughout the trip.",
            ["Nhóm có trẻ em cần ít nhất một người lớn từ 18 tuổi đi cùng."] = "Groups with children must include at least one adult aged 18 or over.",
            ["Vui lòng chọn một chỗ ngồi trước khi sang bước thanh toán."] = "Select a seat before going to checkout.",
            ["Ghế bạn chọn vừa được hành khách khác giữ. Vui lòng chọn ghế khác."] = "Another passenger just held your selected seat. Select another one.",
            ["Vui lòng chọn một ghế còn trống."] = "Select an available seat.",
            ["Chỗ hoặc hành khách vừa được đặt ở phiên khác. Vui lòng kiểm tra và chọn lại."] = "The seat or passenger was just booked in another session. Review and select again.",
            ["Ghế hoặc hành khách vừa được đặt ở phiên khác. Vui lòng chọn lại."] = "The seat or passenger was just booked in another session. Select again.",
            ["Ghế đã có khách hoặc được chọn trùng trong nhóm. Vui lòng chọn lại."] = "The seat is occupied or was selected twice in your group. Select again.",
            ["Hành khách đã có vé hoặc đang giữ chỗ trên một chuyến trong hành trình."] = "The passenger already has a ticket or hold on one of these flights.",
            ["Giấy tờ đã được sử dụng với thông tin khác. Kiểm tra họ tên và ngày sinh."] = "This ID was used with different details. Check the name and date of birth.",
            ["Vui lòng chọn phương thức thanh toán hợp lệ."] = "Select a valid payment method.",
            ["Đặt chỗ đã có giao dịch đang chờ. Hãy tiếp tục với phương thức đã chọn."] = "A transaction is pending for this booking. Continue with the selected payment method.",
            ["Thông tin liên hệ đã thay đổi. Vui lòng quay lại bước hành khách để xác nhận."] = "The contact details changed. Go back to the passenger step to confirm them.",
            ["Tổng tiền không hợp lệ. Vui lòng tải lại trang."] = "The total is invalid. Reload the page.",
            ["Chọn một thẻ mẫu được cung cấp trong danh sách."] = "Choose one of the available test cards.",
            ["Thanh toán mô phỏng chỉ nhận thẻ thử 4242 4242 4242 4242 hoặc 5555 5555 5555 4444."] = "The simulator accepts test cards 4242 4242 4242 4242 and 5555 5555 5555 4444 only.",
            ["Vui lòng nhập tên chủ thẻ."] = "Enter the cardholder's name.",
            ["Ngày hết hạn chưa đúng định dạng MM/YY."] = "Enter the expiry date in MM/YY format.",
            ["Năm hết hạn không hợp lệ."] = "Invalid expiry year.",
            ["Thẻ đã hết hạn. Vui lòng dùng thẻ còn hiệu lực."] = "Use a card that has not expired.",
            ["Mã CVV/CVC phải gồm 3 hoặc 4 chữ số."] = "The CVV/CVC must contain 3 or 4 digits.",
            ["Đơn đang chờ đối soát chuyển khoản. Liên hệ hỗ trợ để thay đổi đơn."] = "The bank transfer is awaiting verification. Contact support to change this booking.",
            ["Chỉ có thể sửa mã trên đặt chỗ còn hạn và chưa tạo giao dịch."] = "You can change the code while the hold is active and before starting payment.",
            ["Mã ưu đãi không hợp lệ."] = "Invalid promotion code.",
            ["Mã ưu đãi đã hết hạn."] = "The promotion code expired.",
            ["Đơn cần có phí dịch vụ và tổng trước ưu đãi từ 3.000.000 ₫."] = "The booking must include a service fee and total at least 3,000,000 VND before the discount.",
            ["Đặt chỗ đã hết hạn hoặc chuyến bay không còn mở bán. Vui lòng đặt lại."] = "Your hold expired or the flight is no longer on sale. Book again.",
            ["Đơn đang có giao dịch chờ xác nhận. Vui lòng tiếp tục với phương thức đã chọn."] = "A transaction is pending. Continue using the selected method.",
            ["Tổng tiền đã thay đổi. Vui lòng kiểm tra lại trước khi thanh toán."] = "The total changed. Review it before paying.",
            ["Không xác minh được kết quả trả về. Kiểm tra trạng thái đặt chỗ."] = "The payment result could not be verified. Check your booking status.",
            ["Mở Tra cứu đặt chỗ bằng PNR và email để kiểm tra kết quả."] = "Look up your booking using its reference and email address to check the result.",
            ["Giao dịch đã được hoàn. Đặt chỗ này không phát hành vé."] = "The transaction was refunded. This booking did not issue tickets.",
            ["VNPAY đã ghi nhận tiền nhưng không thể phát hành vé. Giao dịch đang chờ xử lý hoàn tiền."] = "VNPAY received the payment, but tickets could not be issued. The refund is pending.",
            ["VNPAY không xác nhận thanh toán thành công. Bạn có thể thử lại nếu đặt chỗ còn hạn."] = "VNPAY did not confirm payment. You can retry while the hold is active.",
            ["Đang chờ máy chủ VNPAY xác nhận. Nếu đã thanh toán, hãy kiểm tra lại trạng thái trước khi tạo đặt chỗ mới."] = "Awaiting confirmation from VNPAY. If you already paid, check the status before creating another booking.",
        };

    // Translate presentation text only. Booking identifiers, codes and stored messages keep their original values.
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (!UiLanguage.IsEnglish) return value;
        if (EnglishLabels.TryGetValue(value, out var label)) return label;

        const string seatPrefix = "Chỗ ngồi · ";
        const string cardPrefix = "Thẻ ";
        const string companionPrefix = "Đi cùng ";
        const string ageSuffix = " Nhóm tuổi phải phù hợp ở cả hai chiều.";
        const string seatTakenSuffix = " vừa được hành khách khác chọn. Vui lòng chọn ghế khác.";
        if (value.StartsWith(seatPrefix, StringComparison.Ordinal))
        {
            var details = value[seatPrefix.Length..];
            var location = details.LastIndexOf(" (", StringComparison.Ordinal);
            return location < 0 ? $"Seat · {UiLanguage.Label(details)}" : $"Seat · {Text(details[..location])}{details[location..]}";
        }
        if (value.StartsWith(cardPrefix, StringComparison.Ordinal)) return $"{value[cardPrefix.Length..]} card";
        if (value.StartsWith(companionPrefix, StringComparison.Ordinal))
        {
            var name = value[companionPrefix.Length..];
            return name == "người lớn" ? "Travels with an adult" : $"Travels with {name}";
        }
        if (value.EndsWith(ageSuffix, StringComparison.Ordinal))
            return $"{Text(value[..^ageSuffix.Length])} Age requirements must be met for both flights.";
        if (value.StartsWith("Ghế ", StringComparison.Ordinal) && value.EndsWith(seatTakenSuffix, StringComparison.Ordinal))
            return $"Seat {value[4..^seatTakenSuffix.Length]} was just selected by another passenger. Choose another seat.";
        if (value.Contains(" kg xách tay", StringComparison.Ordinal) || value.Contains(" kg ký gửi", StringComparison.Ordinal))
            return value.Replace(" kg xách tay", " kg carry-on", StringComparison.Ordinal)
                .Replace(" kg ký gửi", " kg checked baggage", StringComparison.Ordinal);

        return UiLanguage.Label(value);
    }

    public static string Party(PassengerParty party) => UiLanguage.IsEnglish
        ? string.Join(", ", new[]
        {
            party.Adults > 0 ? Count(party.Adults, "adult", "adults") : null,
            party.Children > 0 ? Count(party.Children, "child", "children") : null,
            party.Infants > 0 ? Count(party.Infants, "infant", "infants") : null
        }.Where(value => value is not null))
        : party.Summary;

    public static string SearchSummary(FlightSearchCriteria criteria) =>
        $"{Text(criteria.TripTypeText)} · {criteria.DepartDateText} · {Party(criteria.EffectiveParty)} · {Text(criteria.Cabin)}";

    public static string LegCount(int legs) =>
        UiLanguage.T($"{legs} chặng", Count(legs, "flight", "flights"));

    public static string LongDate(DateTimeOffset value) => UiLanguage.IsEnglish
        ? value.ToString("dddd, dd/MM/yyyy", CultureInfo.InvariantCulture)
        : Money.LongDate(value);

    public static string DateTimeText(string value) => UiLanguage.IsEnglish
        ? value.Replace(" ngày ", " on ", StringComparison.Ordinal)
        : value;

    public static string Duration(TimeSpan value)
    {
        if (!UiLanguage.IsEnglish) return Money.Duration(value);
        var hours = (int)value.TotalHours;
        return hours == 0 ? $"{value.Minutes} min" : value.Minutes == 0 ? $"{hours} hr" : $"{hours} hr {value.Minutes} min";
    }

    public static string HoldRemaining(string value) => UiLanguage.IsEnglish &&
        value.StartsWith("Còn ", StringComparison.Ordinal) && value.EndsWith(" phút", StringComparison.Ordinal)
            ? $"{value[4..^5]} min left"
            : value;

    private static string Count(int value, string singular, string plural) =>
        $"{value} {(value == 1 ? singular : plural)}";
}
