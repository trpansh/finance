﻿using System;
using System.Threading.Tasks;
using Frapid.ApplicationState.Models;
using Frapid.Configuration;
using Frapid.Configuration.Db;
using Frapid.Framework.Extensions;
using MixERP.Finance.DTO;
using MixERP.Finance.ViewModels;

namespace MixERP.Finance.DAL
{
    public static class TransacitonPostings
    {
        public static async Task<long> Add(string tenant, LoginView userInfo, TransactionPosting model)
        {
            long transactionMasterId = 0;

            using (var db = DbProvider.Get(FrapidDbServer.GetConnectionString(tenant), tenant).GetDatabase())
            {
                try
                {
                    db.BeginTransaction();

                    var master = new TransactionMaster
                    {
                        Book = "Journal Entry",
                        ValueDate = model.ValueDate,
                        BookDate = model.BookDate,
                        TransactionTs = DateTimeOffset.UtcNow,
                        TransactionCode = string.Empty,
                        LoginId = userInfo.LoginId,
                        UserId = userInfo.UserId,
                        OfficeId = userInfo.OfficeId,
                        CostCenterId = model.CostCenterId,
                        ReferenceNumber = model.ReferenceNumber,
                        StatementReference = string.Empty,
                        VerificationStatusId = 0,
                        VerificationReason = string.Empty,
                        AuditUserId = userInfo.UserId,
                        AuditTs = DateTimeOffset.UtcNow,
                        Deleted = false
                    };

                    var insertedId =
                        await
                            db.InsertAsync("finance.transaction_master", "transaction_master_id", true, master)
                                .ConfigureAwait(true);

                    transactionMasterId = insertedId.To<long>();


                    foreach (var line in model.Details)
                    {
                        decimal amountInCurrency;
                        string tranType;
                        decimal amountInLocalCurrency;

                        if (line.Credit.Equals(0) && line.Debit > 0)
                        {
                            tranType = "Dr";
                            amountInCurrency = line.Debit;
                            amountInLocalCurrency = line.LocalCurrencyDebit;
                        }
                        else
                        {
                            tranType = "Cr";
                            amountInCurrency = line.Credit;
                            amountInLocalCurrency = line.LocalCurrencyCredit;
                        }


                        var detail = new TransactionDetail
                        {
                            TransactionMasterId = transactionMasterId,
                            ValueDate = model.ValueDate,
                            BookDate = model.BookDate,
                            TranType = tranType,
                            AccountId = await Accounts.GetAccountIdByAccountNumberAsync(tenant, line.AccountNumber),
                            StatementReference = line.StatementReference,
                            CashRepositoryId = await CashRepositories.GetCashRepositoryIdByCashRepositoryCodeAsync(tenant, line.CashRepositoryCode),
                            CurrencyCode = line.CurrencyCode,
                            AmountInCurrency = amountInCurrency,
                            OfficeId = userInfo.OfficeId,
                            LocalCurrencyCode = userInfo.CurrencyCode,
                            Er = line.ExchangeRate,
                            AmountInLocalCurrency = amountInLocalCurrency,
                            AuditUserId = userInfo.UserId,
                            AuditTs = DateTimeOffset.UtcNow,
                        };

                        await db.InsertAsync("finance.transaction_details", "transaction_detail_id", true, detail);
                    }

                    if (model.Attachemnts != null && model.Attachemnts.Count > 0)
                    {
                        foreach (var item in model.Attachemnts)
                        {
                            var document = new TransactionDocument
                            {
                                TransactionMasterId = transactionMasterId,
                                OriginalFileName = item.OriginalFileName,
                                FileExtension = item.FileExtension,
                                FilePath = item.FilePath,
                                Memo = item.Memo,
                                AuditUserId = userInfo.UserId,
                                AuditTs = DateTimeOffset.UtcNow,
                                Deleted = false
                            };

                            await db.InsertAsync("finance.transaction_documents", "document_id", true, document);
                        }
                    }

                    db.CompleteTransaction();
                }
                catch (Exception)
                {
                    db.AbortTransaction();
                    throw;
                }
            }

            return transactionMasterId;
        }
    }
}