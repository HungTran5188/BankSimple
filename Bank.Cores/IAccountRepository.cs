
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Bank.Entities;
using System.Threading.Tasks;
using Bank.Infrastructures;
using Bank.Models;
using Bank.Dto;
using System.Threading;

namespace Bank.Cores
{
    public interface IAccountRepository
    {
        Task<List<Account>> GetAll();

        Task<List<AccountTransaction>> GetTransactions();
        Task<Account> Get(int? id);
        Account Get(string accountNumber);
        Task<int> Add(Account entity);

        Task<int> Delete(int id);
        Task<int> DepositAmount(byte[] rowVersion, AccountEditModel model);
        Task<int> WithdrawAmount(byte[] rowVersion, AccountEditModel model);
       
        void TranferAmount(byte[] rowVersion, AccountEditModel model, Account senderEntity, Account receiverEntity);
        bool IsValidAccount(AccountEditModel model, out Account senderEntity, out Account receiverEntity);

        void ConcurencyTest();
    }
    public class AccountRepository : IAccountRepository
    {
        private readonly BankContext _context;
        private static readonly object _obj = new object();
        public AccountRepository(BankContext context)
        {
            _context = context;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public Task<int> Add(Account entity)
        {
            if (_context.Accounts.SingleOrDefault(x => x.AccountNumber == entity.AccountNumber.Trim()) != null)
            {
                throw new BankException("Existing Account Number.");
            }
            entity.Password = CommonFunction.CreatePasswordHash(entity.Password, Conts.PASSWORD_SALT);
            entity.CreatedDate = DateTime.Now;
            _context.Add(entity);
            _context.Entry(entity).State = EntityState.Added;
            return _context.SaveChangesAsync();
        }

        public Task<int> Delete(int id)
        {
            var account = _context.Accounts.SingleOrDefault(x => x.AccountID == id);
            _context.Accounts.Remove(account);
            return _context.SaveChangesAsync();

        }

        public Task<Account> Get(int? id)
        {

            return _context.Accounts.SingleOrDefaultAsync(x => x.AccountID == id);
        }
        public Account Get(string accountNumber)
        {

            return _context.Accounts.SingleOrDefault(x => x.AccountNumber == accountNumber);
        }

        public Task<List<Account>> GetAll()
        {
            return _context.Accounts.OrderByDescending(x=>x.CreatedDate).ToListAsync();
        }
        public Task<List<AccountTransaction>> GetTransactions()
        {
            return _context.AccountTransactions.Include(x => x.Account).OrderByDescending(x=>x.TranDate).ToListAsync();

        }
        public Task<int> DepositAmount(byte[] rowVersion, AccountEditModel model)
        {
            AccountTransaction trans = null;
            Account entity = null;

            lock (_obj)
            {
                entity = _context.Accounts.SingleOrDefault(x => x.AccountID == model.AccountID);
                if (entity == null)
                {
                    throw new BankException("Unable to deposit amount. Account Number was deleted by another user.");
                }

                _context.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
                
                BankAccount ac = new BankAccount(entity.Balance);
                ac.Deposit(model.Amount);
                entity.Balance = ac.Balance;

                trans = new AccountTransaction()
                {
                    AccountID = entity.AccountID,
                    Description = string.Format("Successfull deposit amount: {0}. Total balance: {1}. Account Number: {2}", model.Amount.ToString(), entity.Balance.ToString(), entity.AccountNumber),
                    TranDate = DateTime.Now,

                };
                entity.AccountTransactions = new[] { trans };
                _context.Entry(trans).State = EntityState.Added;

                _context.Entry(entity).State = EntityState.Modified;
            }
            return _context.SaveChangesAsync();

        }

        public Task<int> WithdrawAmount(byte[] rowVersion, AccountEditModel model)
        {
            AccountTransaction trans = null;
            Account entity = null;
            Task<int> result;
            lock (_obj)
            {
                entity = _context.Accounts.SingleOrDefault(x => x.AccountID == model.AccountID);
                if (entity == null)
                {
                    throw new BankException("Unable to withdraw amount. Account Number was deleted by another user.");
                }


                _context.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

                trans = new AccountTransaction()
                {
                    AccountID = entity.AccountID,

                    TranDate = DateTime.Now,

                };

                var withdrawAccount = new BankAccount(entity.Balance);
                if (withdrawAccount.Withdraw(model.Amount))
                {
                    entity.Balance = withdrawAccount.Balance;
                    trans.Description = string.Format("Successfull Withdraw amount: {0}. Total balance: {1}. Account Number {2}", model.Amount.ToString(), entity.Balance.ToString(), entity.AccountNumber);
                    entity.AccountTransactions = new[] { trans };
                    _context.Entry(trans).State = EntityState.Added;
                    _context.Entry(entity).State = EntityState.Modified;
                    result = _context.SaveChangesAsync();
                
                }
                else
                {
                    trans.Description = string.Format("Unable to withdraw amount: {0}. Insufficient balance. Account Number {1}", model.Amount.ToString(), entity.AccountNumber);
                    _context.Entry(trans).State = EntityState.Added;
                    result = _context.SaveChangesAsync();
                    throw new BankException("Unable to withdraw amount. Insufficient balance.");
                }
            }
            return result;


        }
       
        public void TranferAmount(byte[] rowVersion, AccountEditModel model, Account senderEntity, Account receiverEntity)
        {
            AccountTransaction trans = null;
            using (var transaction = _context.Database.BeginTransaction())
            {
            Monitor.Enter(_obj);
            try
            {
              
                var receiverAccount = new BankAccount(receiverEntity.Balance);
                receiverAccount.Deposit(model.Amount);
                receiverEntity.Balance = receiverAccount.Balance;
             
                _context.Entry(senderEntity).State = EntityState.Modified;

                _context.Entry(receiverEntity).State = EntityState.Modified;

                _context.Entry(senderEntity).Property("RowVersion").OriginalValue = rowVersion;
                trans = new AccountTransaction()
                {
                    AccountID = senderEntity.AccountID,
                    Description = string.Format("Successfull tranfer amount: {0} from {1} to {2}", model.Amount.ToString(), senderEntity.AccountNumber, receiverEntity.AccountNumber),
                    TranDate = DateTime.Now,

                };
                senderEntity.AccountTransactions = new[] { trans };
                _context.Entry(trans).State = EntityState.Added;
                _context.SaveChanges();
                 transaction.Commit();
            }

            catch (Exception ex)
            {
                transaction.Rollback();
               
                throw new BankException(ex.Message);
            }
            finally { Monitor.Exit(_obj); }

            }

        }

        public bool IsValidAccount(AccountEditModel model, out Account senderEntity, out Account receiverEntity)
        {

            senderEntity = _context.Accounts.SingleOrDefault(x => x.AccountID == model.AccountID);
            if (senderEntity == null)
            {
                throw new BankException("Unable to tranfer amount. Sender Account Number was deleted by another user.");
            }
            receiverEntity = _context.Accounts.SingleOrDefault(x => x.AccountNumber == model.TranferNumber.Trim() && x.AccountID != model.AccountID);
            if (receiverEntity == null)
            {
                throw new BankException("Not found tranfer number.");
            }

            var senderAccount = new BankAccount(senderEntity.Balance);
            if (!senderAccount.Withdraw(model.Amount))
            {
                throw new BankException("Unable to tranfer amount. Insufficient balance!");
            }
            else
            {
                senderEntity.Balance = senderAccount.Balance;
            }
            return true;
        }

        public void ConcurencyTest()
        {
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
            _context.Accounts.Add(new Account { AccountNumber = "987651234", AccountName = "Test", Balance = 350000, CreatedDate = DateTime.Now });
            _context.SaveChanges();

            var builder = new DbContextOptionsBuilder<BankContext>();
            builder.UseSqlServer(@"Server=.;Database=Bank;Trusted_Connection=True;");

            DbContextOptions<BankContext> options = builder.Options;

            using (var _context = new BankContext(options))
            {
                var databaseAccount = _context.Accounts.Where(x => x.AccountNumber == "987651234").FirstOrDefault();
                databaseAccount.CreatedDate = DateTime.Now;
                // Change the persons name in the database (will cause a concurrency conflict)
                 _context.Database.ExecuteSqlCommand("UPDATE dbo.Accounts SET Balance = 10 WHERE AccountID = 1"); //AccountNumber : 56789
                _context.Entry(databaseAccount).State = EntityState.Modified;
                    _context.SaveChanges();
               

            }

        }
    }
}
