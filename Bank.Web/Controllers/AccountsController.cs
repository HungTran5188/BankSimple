using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Bank.Entities;
using Bank.Cores;
using Bank.Models;
using Bank.Infrastructures;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;

namespace Bank.Web.Controllers
{
    /// <summary>
    /// Assumption user after login successful and Use the Authorize to prevent Anonymous access this page
    /// </summary>
    //[Authorize]
    public class AccountsController : BaseController
    {
       // private IMapper _mapper { get; set; }
        private readonly IAccountRepository _accountRepository;
        public AccountsController(IAccountRepository accountRepository, IMapper mapper)
        {
            _accountRepository = accountRepository;
            //_mapper = mapper;
        }

        // GET: Accounts
        public async Task<IActionResult> Index()
        {
            return View(await _accountRepository.GetAll());
        }
        public async Task<IActionResult> Transactions()
        {
            return View(await _accountRepository.GetTransactions());
        }
        // GET: Accounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _accountRepository.Get(id);
            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }

        // GET: Accounts/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Accounts/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        /// <summary>
        /// Use AccountModel instead of Account for validation model
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AccountNumber,AccountName,Password,ConfirmPassword, Balance")] AccountCreateModel accountModel)
        {
            try
            {
                //Use ModelState to prevent the external incomming attack, rules validate in DataAnnotations of AccountCreateModel class
                if (ModelState.IsValid)
                {
                    //Move mapping to extenstions function or using mapper to map
                    // Account account = _mapper.Map<AccountCreateModel, Account>(accountModel);
                    Account account = new Account()
                    {
                        AccountNumber = accountModel.AccountNumber,
                        AccountName = accountModel.AccountName,
                        Password = accountModel.Password,
                        Balance = accountModel.Balance,
                    };

                    await _accountRepository.Add(account);
                    return RedirectToAction("Index");
                }
            }
            catch (BankException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }


            return View(accountModel);
        }

        // GET: Accounts/Edit/5
        public async Task<IActionResult> Deposit(int? id)
        {
            return await GetEditModel(id, ActionType.Deposit);
        }

        public async Task<IActionResult> Withdraw(int? id)
        {
            return await GetEditModel(id, ActionType.Withdraw);
        }
        public async Task<IActionResult> Tranfer(int? id)
        {
            return await GetEditModel(id, ActionType.Tranfer);
        }
        private async Task<IActionResult> GetEditModel(int? id, ActionType type)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _accountRepository.Get(id);
            if (account == null)
            {
                return NotFound();
            }

            return View("Edit", new AccountEditModel()
            {
                AccountID = account.AccountID,
                AccountName = account.AccountName,
                AccountNumber = account.AccountNumber,
                RowVersion = account.RowVersion,
                Balance = account.Balance,
                ActionType = type,

            });
        }



        // POST: Accounts/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(byte[] rowVersion, ActionType ActionType, [Bind("AccountID,AccountNumber,AccountName,Balance,Amount,TranferNumber")] AccountEditModel account)
        {
            try
            {
                switch (ActionType)
                {
                    case ActionType.Deposit:
                        await _accountRepository.DepositAmount(rowVersion, account);
                        break;
                    case ActionType.Withdraw:
                        await _accountRepository.WithdrawAmount(rowVersion, account);
                       
                        break;
                    case ActionType.Tranfer:
                        Account senderAccount = null;
                        Account reciverAccount = null;

                        if (_accountRepository.IsValidAccount(account, out senderAccount, out reciverAccount))
                        {
                            await Task.Run(() => { _accountRepository.TranferAmount(rowVersion, account, senderAccount, reciverAccount); });
                        }
                        break;
                    default:
                        break;
                }



                return RedirectToAction("Index");
            }
            //catch (DbUpdateConcurrencyException ex)
            //{

            //    ModelState.AddModelError(string.Empty, "The record you attempted to edit "
            //        + "was modified by another user after you got the original value.");
            //    account.RowVersion = rowVersion;
            //}
            catch (BankException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            account.ActionType = ActionType;
            account.RowVersion = rowVersion;
            return View(account);
        }

        // GET: Accounts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _accountRepository.Get(id);
            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }

        // POST: Accounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _accountRepository.Delete(id);
            return RedirectToAction("Index");
        }

        private bool AccountExists(int id)
        {
            var result = _accountRepository.Get(id);
            return (result.IsCompleted && result.Result != null);
        }

        public IActionResult ConcurencyTest()
        {
            _accountRepository.ConcurencyTest();
            return Ok();
        }
    }
}
