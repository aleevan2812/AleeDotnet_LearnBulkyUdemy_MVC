using System.Security.Claims;
using AleeBook.DataAccess.Repository.IRepository;
using AleeBook.Models;
using AleeBook.Models.ViewModels;
using AleeBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AleeBookWeb.Areas.Admin.Controllers;

[Area("admin")]
[Authorize]
public class OrderController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public OrderController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty] public OrderVM OrderVM { get; set; }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Details(int orderId)
    {
        OrderVM = new OrderVM
        {
            OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, "ApplicationUser"),
            OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, "Product")
        };
        return View(OrderVM);
    }

    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult UpdateOrderDetail(int orderId)
    {
        var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);

        orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
        orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
        orderHeaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
        orderHeaderFromDb.City = OrderVM.OrderHeader.City;
        orderHeaderFromDb.State = OrderVM.OrderHeader.State;
        orderHeaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;

        if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
            orderHeaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
        if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
            orderHeaderFromDb.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;

        _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
        _unitOfWork.Save();

        TempData["Success"] = "Order Details Updated Successfully";

        return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
    }

    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult StartProcessing()
    {
        _unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
        _unitOfWork.Save();
        TempData["Success"] = "Order Details Updated Successfully";

        return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
    }
    
    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult ShipOrder()
    {
        var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
        orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
        orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
        orderHeader.OrderStatus = SD.StatusShipped;
        orderHeader.ShippingDate = DateTime.Now;
        if(orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
            orderHeader.PaymentDueDate = (DateTime.Now.AddDays(30));
        _unitOfWork.OrderHeader.Update(orderHeader);
        _unitOfWork.Save();
        
        TempData["Success"] = "Order Shipped Successfully";
    
        return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
    }


    #region API CALLS

    [HttpGet]
    public IActionResult GetAll(string status)
    {
        IEnumerable<OrderHeader> objOrderHeaders;

        if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
        {
            objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();
        }
        else
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            // noi luu tru userId

            objOrderHeaders = _unitOfWork.OrderHeader.GetAll(u => u.ApplicationUserId == userId,
                "ApplicationUser");
        }

        switch (status)
        {
            case "pending":
                objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                break;
            case "inprocess":
                objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusInProcess);
                break;
            case "completed":
                objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusShipped);
                break;
            case "approved":
                objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusApproved);
                break;
        }

        return Json(new { data = objOrderHeaders });
    }

    #endregion
}