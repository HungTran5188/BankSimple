using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bank.Entities
{
    public class BankContext : DbContext
    {
        public BankContext(DbContextOptions<BankContext> options)
          : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>().Property(s => s.Balance).IsConcurrencyToken();
            base.OnModelCreating(modelBuilder);
        }

        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<AccountTransaction> AccountTransactions { get; set; }
        

    }

    public partial class Account
    {

        public Account()
        {
            AccountTransactions = new HashSet<AccountTransaction>();
        }
        [Key]
        public int AccountID { get; set; }

        public string AccountNumber { get; set; }

        public string AccountName { get; set; }

     
        public string Password { get; set; }

        [ConcurrencyCheck]
        public decimal Balance { get; set; }

        public DateTime? CreatedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
        public virtual ICollection<AccountTransaction> AccountTransactions { get; set; }
    }

    public partial class AccountTransaction
    {
        [Key]
        public int AccountTransactionID { get; set; }

        public string Description { get; set; }

        public DateTime? TranDate { get; set; }

        public int? AccountID { get; set; }

        public virtual Account Account { get; set; }
    }
}
