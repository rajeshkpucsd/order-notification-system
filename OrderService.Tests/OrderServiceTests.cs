using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using OrderService.DTOs;
using OrderService.Events;
using OrderService.Messaging;
using OrderService.Repositories;
namespace OrderService.Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IOrderRepository> _repoMock;
        private readonly Mock<IRabbitMqPublisher> _publisherMock;
        private readonly Mock<ILogger<OrderService.Services.OrderService>> _loggerMock;
        private readonly OrderService.Services.OrderService _service;

        public OrderServiceTests()
        {
            _repoMock = new Mock<IOrderRepository>();
            _publisherMock = new Mock<IRabbitMqPublisher>();
            _loggerMock = new Mock<ILogger<OrderService.Services.OrderService>>();

            _service = new OrderService.Services.OrderService(
                _repoMock.Object,
                _publisherMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task CreateOrder_Should_Save_Order_And_Publish_Event()
        {            
            var dto = new CreateOrderDto
            {
                CustomerEmail = "testtest.com",
                ProductCode = "P100",
                Quantity = -1
            };

            var result = await _service.CreateOrderAsync(dto);

            _repoMock.Verify(r => r.AddAsync(It.IsAny<Models.Order>()), Times.Once);

            _publisherMock.Verify(p =>
                p.Publish(It.IsAny<OrderCreatedEvent>(), "order-created"),
                Times.Once);

            result.Order.Should().NotBeNull();
            result.EventPublished.Should().BeTrue();
        }
    }
}
