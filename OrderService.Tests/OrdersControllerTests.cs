using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrderService.Controllers;
using OrderService.DTOs;
using OrderService.Models;
using OrderService.Responses;
using OrderService.Services;

namespace OrderService.Tests;

public class OrdersControllerTests
{
    private readonly Mock<IOrderService> _serviceMock;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _serviceMock = new Mock<IOrderService>();
        _controller = new OrdersController(_serviceMock.Object);
    }

    [Fact]
    public async Task Create_When_Event_Published_Returns_Ok()
    {
        var dto = new CreateOrderDto
        {
            CustomerEmail = "customer@example.com",
            ProductCode = "P100",
            Quantity = 2
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerEmail = dto.CustomerEmail,
            ProductCode = dto.ProductCode,
            Quantity = dto.Quantity
        };

        _serviceMock
            .Setup(s => s.CreateOrderAsync(dto))
            .ReturnsAsync(new OrderResult { Order = order, EventPublished = true });

        var result = await _controller.Create(dto);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = ok.Value.Should().BeOfType<ApiResponse<Order>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().Be(order);
        response.Message.Should().Be("Order created successfully.");
    }

    [Fact]
    public async Task Create_When_Event_Not_Published_Returns_Accepted()
    {
        var dto = new CreateOrderDto
        {
            CustomerEmail = "customer@example.com",
            ProductCode = "P200",
            Quantity = 1
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerEmail = dto.CustomerEmail,
            ProductCode = dto.ProductCode,
            Quantity = dto.Quantity
        };

        _serviceMock
            .Setup(s => s.CreateOrderAsync(dto))
            .ReturnsAsync(new OrderResult { Order = order, EventPublished = false });

        var result = await _controller.Create(dto);

        var accepted = result.Should().BeOfType<ObjectResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);

        var response = accepted.Value.Should().BeOfType<ApiResponse<Order>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().Be(order);
        response.Message.Should().Be("Order saved, but event publish failed. Notification may be delayed.");
    }

    [Fact]
    public async Task GetAll_Returns_Ok_With_Orders()
    {
        var orders = new List<Order>
        {
            new Order { Id = Guid.NewGuid(), CustomerEmail = "a@example.com", ProductCode = "P1", Quantity = 1 },
            new Order { Id = Guid.NewGuid(), CustomerEmail = "b@example.com", ProductCode = "P2", Quantity = 3 }
        };

        _serviceMock
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync(orders);

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = ok.Value.Should().BeOfType<ApiResponse<List<Order>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(orders, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetById_Returns_Ok_With_Order()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            CustomerEmail = "c@example.com",
            ProductCode = "P3",
            Quantity = 4
        };

        _serviceMock
            .Setup(s => s.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        var result = await _controller.GetById(orderId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = ok.Value.Should().BeOfType<ApiResponse<Order>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().Be(order);
    }
}
