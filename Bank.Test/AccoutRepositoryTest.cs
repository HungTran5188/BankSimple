using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bank.Cores;
using Bank.Entities;
using Microsoft.EntityFrameworkCore;
using Bank.Infrastructures;
using Bank.Models;

namespace Bank.Test
{
    [TestClass]
    public class AccoutRepositoryTest
    {
        private  IAccountRepository _accountRepository;
       

        [TestInitialize]
        public void SetUp()
        {
         
            var builder = new DbContextOptionsBuilder<BankContext>();
            builder.UseSqlServer(@"Server=.;Database=Bank;Trusted_Connection=True;");
        
            DbContextOptions<BankContext> options = builder.Options;

           _accountRepository = new AccountRepository(new BankContext(options));
        }

        #region Account
        [TestMethod]
        public void InsertAccount_DuplicateAccountNumber()
        {
            Account entity = new Account()
            {
                AccountNumber = "1234567",
                AccountName = "Abc",
                Balance = 150000,
                CreatedDate = DateTime.Now,
            };

            Assert.ThrowsExceptionAsync<BankException>(() => _accountRepository.Add(entity));
        }
        [TestMethod]
        public  void InsertAccount_Success()
        {
            Account entity = new Account()
            {
                AccountNumber = "5678912",
                AccountName = "Test",
                Balance = 150000,
                Password = CommonFunction.CreatePasswordHash("12345", Conts.PASSWORD_SALT),
                CreatedDate = DateTime.Now,
            };
            var result =  _accountRepository.Add(entity);
            Assert.AreNotEqual(0, result.Result);
        }
        #endregion
        [TestMethod]
        #region Deposit
        public void DepositAmount_Success()
        {
            var account = _accountRepository.Get("1234567");
            AccountModel model = new AccountModel()
            {
                AccountID = account.AccountID,
                Amount = 15000,
            };
           var result =  _accountRepository.DepositAmount(account.RowVersion, model);

            Assert.AreNotEqual(0, result.Result);
        }

        [TestMethod]
        public  void DepositAmount_Concurency()
        {
            var account = _accountRepository.Get("1234567");
            AccountModel model = new AccountModel()
            {
                AccountID = account.AccountID,
                Amount = 15000,
            };
            //var result = _accountRepository.DepositAmount(new byte[] { }, model);

            Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => _accountRepository.DepositAmount(new byte[] { }, model));
        }
        #endregion

        #region Withdraw
        [TestMethod]
        public void WithdrawAmount_Success()
        {
            var account = _accountRepository.Get("1234567");
            AccountModel model = new AccountModel()
            {
                AccountID = account.AccountID,
                Amount = 300000,
            };
            var result = _accountRepository.WithdrawAmount(account.RowVersion, model);

            Assert.AreNotEqual(0, result.Result);
        }
        [TestMethod]
        public void WithdrawAmount_Concurency()
        {
            var account = _accountRepository.Get("1234567");
            AccountModel model = new AccountModel()
            {
                AccountID = account.AccountID,
                Amount = 15000,
            };
            
            Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => _accountRepository.WithdrawAmount(new byte[] { }, model));
        }

        [TestMethod]
        public void WithdrawAmount_Insufficient()
        {
            var account = _accountRepository.Get("1234567");
            AccountModel model = new AccountModel()
            {
                AccountID = account.AccountID,
                Amount = 500000000,
            };

            Assert.ThrowsExceptionAsync<BankException>(() => _accountRepository.WithdrawAmount(new byte[] { }, model));
        }
        #endregion

        #region Tranfer
        [TestMethod]
        public void TranferAmount_Success()
        {
            Account senderAccount = null;
            Account reciverAccount = null;
            AccountModel account = new AccountModel()
            {
                AccountID = 1, //Sender Account
                TranferNumber = "123456789",
                Amount = 50000,
            };
            if (_accountRepository.IsValidAccount(account, out senderAccount, out reciverAccount))
            {
                _accountRepository.TranferAmount(senderAccount.RowVersion, account, senderAccount, reciverAccount);
                var result = _accountRepository.Get("123456789");
                Assert.IsInstanceOfType(result, typeof(Account));
            }
        }
        [TestMethod]
        public void TranferAmount_SenderInsufficientBalance()
        {
            Account senderAccount = null;
            Account reciverAccount = null;
            AccountModel account = new AccountModel()
            {
                AccountID = 1, //Sender Account
                TranferNumber = "123456789", //ReciverAccoutNumber
                Amount = 50000000000,
            };
            Assert.ThrowsException<BankException>(() => _accountRepository.IsValidAccount(account, out senderAccount, out reciverAccount));
          
        }
        [TestMethod]
        public void TranferAmount_InvalidAccountNumber()
        {
            Account senderAccount = null;
            Account reciverAccount = null;
            AccountModel account = new AccountModel()
            {
                AccountID = 1, //Sender Account
                TranferNumber = "1234567892126537678", //InvalidAccountNumber
                Amount = 20000,
            };
            Assert.ThrowsException<BankException>(() => _accountRepository.IsValidAccount(account, out senderAccount, out reciverAccount));

        }

        [TestMethod]
        public void TranferAmount_Concurency()
        {
            Account senderAccount = null;
            Account reciverAccount = null;
            AccountModel account = new AccountModel()
            {
                AccountID = 1, //Sender Account
                TranferNumber = "123456789", //ReciverAccoutNumber
                Amount = 20000,
            };
            if (_accountRepository.IsValidAccount(account, out senderAccount, out reciverAccount))
            {
                Assert.ThrowsException<DbUpdateConcurrencyException>(() => _accountRepository.TranferAmount(senderAccount.RowVersion, account, senderAccount, reciverAccount));
            }
            

        }
        #endregion
    }
}
