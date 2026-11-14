using AirlineTicketing.BUS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AirlineTicketing.Web.Infrastructure;

/// <summary>
/// Nạp hotline / email hỗ trợ từ bảng cấu hình hệ thống cho mọi view MVC để các trang
/// công khai không còn hardcode số tổng đài. Nếu đọc cấu hình lỗi thì dùng giá trị mặc định.
/// </summary>
public sealed class ContactInfoFilter(CauHinhHeThongBUS cauHinhHeThong) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is Controller controller)
        {
            try
            {
                var chinhSach = await cauHinhHeThong
                    .LayChinhSachAsync(context.HttpContext.RequestAborted);
                controller.ViewData["SupportHotline"] = chinhSach.Hotline;
                controller.ViewData["SupportEmail"] = chinhSach.SupportEmail;
                controller.ViewData["SupportHotlineTel"] = new string(chinhSach.Hotline.Where(char.IsDigit).ToArray());
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                controller.ViewData["SupportHotline"] = ChinhSachDatVe.DefaultHotline;
                controller.ViewData["SupportEmail"] = ChinhSachDatVe.DefaultSupportEmail;
                controller.ViewData["SupportHotlineTel"] =
                    new string(ChinhSachDatVe.DefaultHotline.Where(char.IsDigit).ToArray());
            }
        }

        await next();
    }
}
