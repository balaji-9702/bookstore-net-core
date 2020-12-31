﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BookstoreProject.Data;
using BookstoreProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BookstoreProject.Controllers
{
    public class BasketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<UserDetails> _userManager;
        private readonly IStringLocalizer<BasketsController> _localizer;
        public BasketsController(ApplicationDbContext context, UserManager<UserDetails> userManager, IStringLocalizer<BasketsController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
        }


        [Authorize]
        public async Task<IActionResult> Index()
        {
            Basket basket = _context.Baskets
                .Where(x => x.UserDetailsId == User.FindFirstValue(ClaimTypes.NameIdentifier) && x.Active == true)
                .FirstOrDefault();

            if(basket == null)
            {
                Basket newBasket = new Basket
                {
                    Status = "YENI",
                    Active = true,
                    UserDetails = await _userManager.GetUserAsync(User)
                };

                _context.Add(newBasket);
                _context.SaveChanges();
            } else
            {
                List<BasketItem> basketItems = await _context.BasketItems
                        .Include(x => x.Basket)
                        .Where(x => x.Basket.UserDetailsId == User.FindFirstValue(ClaimTypes.NameIdentifier) && x.Basket.Active == true && x.Active == true)
                        .Include(x => x.Book).ToListAsync();

                if (basketItems.Count != 0)
                {
                    ViewData["ToplamFiyat"] = basketItems.Sum(x => x.Book.Price);
                    ViewData["BasketID"] = basketItems[0].BasketId;
                    return View(basketItems);
                }
            }

            return View();
        }

        [Authorize]
        public async Task<IActionResult> RemoveFromBasket(int? bookId)
        {
            if (bookId == null)
            {
                return NotFound();
            }

            Basket basket = _context.Baskets
                .Where(x => x.UserDetailsId == User.FindFirstValue(ClaimTypes.NameIdentifier) && x.Active == true)
                .FirstOrDefault();

            if(basket != null)
            {
                BasketItem basketItem = _context.BasketItems
                    .Where(x => x.BasketId == basket.Id && x.Active == true && x.BookId == bookId).FirstOrDefault();

                if(basketItem != null)
                {
                    basketItem.Active = false;
                    _context.Update(basketItem);
                    await _context.SaveChangesAsync();
                }
            } else
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> BuyBooks(int? basketId)
        {
            Basket basket = await _context.Baskets
                .Where(x => x.Id == basketId)
                .FirstOrDefaultAsync();

            if(basket != null)
            {
                List<BasketItem> basketItem = await _context.BasketItems
                    .Include(x => x.Book)
                    .Where(x => x.BasketId == basketId && x.Active == true)
                    .ToListAsync();

                for(int i = 0; i < basketItem.Count; i++)
                {
                    Book _book = basketItem[i].Book;

                    if(_book.Stock == 0)
                    {
                        for(int j = 0; j < i; j++)
                        {
                            Book tmpBook = basketItem[i].Book;
                            tmpBook.Stock++;
                            _context.Update(tmpBook);
                        }

                        TempData["SiparisMesaj"] = _localizer["SiparisMesaj1"]
                            + _book.Name;
                        return RedirectToAction("Index", "Baskets");
                    }

                    _book.Stock = _book.Stock - 1;
                    _context.Update(_book);
                }

                basket.Active = false;
                basket.Status = "KARGO";
                _context.Update(basket);
                await _context.SaveChangesAsync();
            }

            TempData["SiparisMesaj"] = _localizer.GetString("SiparisMesaj2") + basketId;
            //_localizer["SiparisMesaj2"] + " " + basketId;
            return RedirectToAction("Index", "Baskets");
        }
    }
}
