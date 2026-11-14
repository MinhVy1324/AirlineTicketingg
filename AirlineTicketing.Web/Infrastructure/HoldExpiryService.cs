using AirlineTicketing.BUS;

namespace AirlineTicketing.Web.Infrastructure;

/// <summary>
/// Quét định kỳ các đặt chỗ <c>ChoThanhToan</c> đã quá hạn giữ để nhả ghế, kể cả khi
/// không có ai mở trang. Chu kỳ lấy từ <c>Booking:ExpirySweepSeconds</c>; mỗi vòng
/// tạo scope riêng vì <see cref="DatVeBUS"/> và DbContext là scoped.
/// </summary>
public sealed class HoldExpiryService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<HoldExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Clamp(
            configuration.GetValue<int?>("Booking:ExpirySweepSeconds") ?? 60, 15, 3600);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var datVeBUS = scope.ServiceProvider.GetRequiredService<DatVeBUS>();
                var expired = await datVeBUS.HetHanCacDatVeAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation(
                        "Đã giải phóng {Count} đặt chỗ hết hạn giữ.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // CSDL có thể chưa sẵn sàng lúc container khởi động: ghi log và thử lại vòng sau.
                logger.LogError(exception, "Không quét được đặt chỗ hết hạn giữ.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
