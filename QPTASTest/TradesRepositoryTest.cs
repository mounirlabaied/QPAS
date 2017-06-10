﻿// -----------------------------------------------------------------------
// <copyright file="TradeTest.cs" company="">
// Copyright 2014 Alexander Soffronow Pagonidis
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;
using EntityModel;
using Moq;
using NUnit.Framework;
using QPAS;

namespace QPASTest
{
    [TestFixture]
    public class TradesRepositoryTest
    {
        private Instrument _inst;
        private Trade _t;
        private TradesRepository _repository;
        private Mock<IDataSourcer> _dsMock;
        private Mock<IDBContext> _contextMock;
        private DbSetStub<EquitySummary> _dbSetStub;

        private List<QDMS.OHLCBar> GenerateData(DateTime start, DateTime end)
        {
            var data = new List<QDMS.OHLCBar>();
            while(start <= end)
            {
                data.Add(new QDMS.OHLCBar
                    {
                        DT = start,
                        Open = 100,
                        High = 100,
                        Low = 100,
                        Close = 100
                    });
                start = start.AddDays(1);
            }
            return data;
        }

        [SetUp]
        public void SetUp()
        {
            _inst = new Instrument {ID = 1, Multiplier = 1, AssetCategory = AssetClass.Stock};

            _t = new Trade
            {
                Orders = new List<Order>(),
                CashTransactions = new List<CashTransaction>(),
                FXTransactions = new List<FXTransaction>()
            };

            _dsMock = new Mock<IDataSourcer>();

            _dsMock.Setup(x => x.GetData(It.IsAny<Instrument>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<QDMS.BarSize>()))
                .Returns<Instrument, DateTime, DateTime, QDMS.BarSize>((a, b, c, d) => Task.FromResult(GenerateData(b, c)));

            _contextMock = new Mock<IDBContext>();

            _dbSetStub = new DbSetStub<EquitySummary>();
            var equitySummaries = new List<EquitySummary> 
            { 
                new EquitySummary 
                { 
                    Date = new DateTime(2000,1,1),
                    Total = 10000
                }
            };
            _dbSetStub.AddRange(equitySummaries);
            _contextMock.SetupGet(x => x.EquitySummaries).Returns(_dbSetStub);

            _repository = new TradesRepository(_contextMock.Object, _dsMock.Object, 0.1m);
        }

        [Test]
        public void CapitalUseCorrectlyCalculatedAfterOpenClose()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 100, _t.CapitalTotal);
            Assert.AreEqual(10 * 100, _t.CapitalNet);
            Assert.AreEqual(10 * 100, _t.CapitalLong);
            Assert.AreEqual(0, _t.CapitalShort);
        }

        [Test]
        public void CapitalUseCorrectlyCalculatedAfterAdding()
        {
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 100 + 15 * 105, _t.CapitalTotal);
            Assert.AreEqual(-10 * 100 -15 * 105, _t.CapitalNet);
            Assert.AreEqual(0, _t.CapitalLong);
            Assert.AreEqual(10 * 100 + 15 * 105, _t.CapitalShort);
        }

        [Test]
        public void CapitalUseCorrectlyCalculatedAfterReversing()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 100 + 5 * 105, _t.CapitalTotal);
            Assert.AreEqual(10 * 100 - 5 * 105, _t.CapitalNet);
            Assert.AreEqual(10 * 100, _t.CapitalLong);
            Assert.AreEqual(5 * 105, _t.CapitalShort);
        }

        [Test]
        public void CapitalUseCorrectlyCalculatedIncludingFXRate()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 2m, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1.9m, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 100 * 2 + 5 * 105 * 1.9, _t.CapitalTotal);
            Assert.AreEqual(10 * 100 * 2 - 5 * 105 * 1.9, _t.CapitalNet);
            Assert.AreEqual(10 * 100 * 2, _t.CapitalLong);
            Assert.AreEqual(5 * 105 * 1.9, _t.CapitalShort);
        }

        [Test]
        public void RealizedResultDollarsIsCorrectAfterOpenClose()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 5, _t.ResultDollars);
            Assert.AreEqual(10 * 5, _t.ResultDollarsLong);
            Assert.AreEqual(0, _t.ResultDollarsShort);
        }

        [Test]
        public void RealizedResultDollarsIsCorrectAfterAdding()
        {
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(0, _t.ResultDollars);
            Assert.AreEqual(0, _t.ResultDollarsLong);
            Assert.AreEqual(0, _t.ResultDollarsShort);
        }

        [Test]
        public void RealizedResultDollarsIsCorrectAfterReversing()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10.0 * 5, _t.ResultDollars);
            Assert.AreEqual(10.0 * 5, _t.ResultDollarsLong);
            Assert.AreEqual(0, _t.ResultDollarsShort);
        }

        [Test]
        public void RealizedResultPctIsCorrectAfterOpenClose()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10.0 * 5 / (10 * 100), _t.ResultPct);
            Assert.AreEqual(10.0 * 5 / (10 * 100), _t.ResultPctLong);
            Assert.AreEqual(0, _t.ResultPctShort);
        }

        [Test]
        public void RealizedResultPctIsCorrectAfterAdding()
        {
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(0, _t.ResultPct);
            Assert.AreEqual(0, _t.ResultPctLong);
            Assert.AreEqual(0, _t.ResultPctShort);
        }

        [Test]
        public void RealizedResultPctIsCorrectAfterReversing()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10.0 * 5 / (10 * 100 + 5 * 105), _t.ResultPct);
            Assert.AreEqual(10.0 * 5 / (10 * 100), _t.ResultPctLong);
            Assert.AreEqual(0, _t.ResultPctShort);
        }

        [Test]
        public void IsClosableReturnsTrueIfAllPositionsAreZero()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            Assert.IsTrue(_t.IsClosable());
        }

        [Test]
        public void IsClosableReturnsFalseIfThereAreNonzeroPositions()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            Assert.IsFalse(_t.IsClosable());
        }

        [Test]
        public void CommissionsIsTheSumOfCommissions()
        {
            _t.Orders.Add(new Order { Commission = -5, Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Commission = -5, Quantity = -15, Instrument = _inst, Price = 105, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            int commissions = -10;
            int realizedProfit = 5 * 10;
            int unrealizedProfit = 5 * 5;

            Assert.AreEqual(commissions, _t.Commissions);
            Assert.AreEqual(commissions + realizedProfit + unrealizedProfit, _t.TotalResultDollars);
        }

        [Test]
        public void CommissionsSumTakesIntoAccountFXRate()
        {
            _t.Orders.Add(new Order { Commission = -5, Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 2, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.Orders.Add(new Order { Commission = -5, Quantity = -10, Instrument = _inst, Price = 130, FXRateToBase = 1.5m, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1) });

            _repository.UpdateStats(_t, true);

            decimal commissions = -5 * 2m + -5 * 1.5m;
            decimal realizedPnL = 10 * (130 * 1.5m - 100 * 2);

            Assert.AreEqual(commissions, _t.Commissions);
            Assert.AreEqual(commissions + realizedPnL, _t.ResultDollars);
        }

        [Test]
        public void CashTransactionsAreCorrectlyIncludedInPnL()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.CashTransactions.Add(new CashTransaction { Instrument = _inst, InstrumentID = 1, FXRateToBase = 1, Amount = 5, TransactionDate = new DateTime(2000, 1, 2) });

            _dbSetStub.Add(new EquitySummary
                {
                    Date = new DateTime(2000, 1, 2),
                    Total = 10000
                });
            
            _repository.UpdateStats(_t, true);

            Assert.AreEqual(5, _t.ResultDollars);
            Assert.AreEqual(5, _t.ResultDollarsLong);
            Assert.AreEqual(0, _t.ResultDollarsShort);
        }

        [Test]
        public void CashTransactionsAreCorrectlyIncludedInPnLWithShortPosition()
        {
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _t.CashTransactions.Add(new CashTransaction { Instrument = _inst, InstrumentID = 1, FXRateToBase = 1, Amount = -5, TransactionDate = new DateTime(2000, 1, 2) });

            _dbSetStub.Add(new EquitySummary
            {
                Date = new DateTime(2000, 1, 2),
                Total = 10000
            });
            
            _repository.UpdateStats(_t, true);

            Assert.AreEqual(-5, _t.ResultDollars);
            Assert.AreEqual(0, _t.ResultDollarsLong);
            Assert.AreEqual(-5, _t.ResultDollarsShort);
        }

        [Test]
        public void CapitalUsageOfOptionsIsCorrectlyAdjusted()
        {
            _inst.AssetCategory = AssetClass.Option;
            _inst.Multiplier = 100;
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 5, Multiplier = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1) });
            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 5 * 100 * 0.1m, _t.CapitalTotal);
        }

        [Test]
        public void UnrealizedDollarResultIsCorrectlyCalculated()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 95, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(0, _t.ResultDollars);
            Assert.AreEqual(50, _t.UnrealizedResultDollars);
            Assert.AreEqual(50, _t.UnrealizedResultDollarsLong);
        }

        [Test]
        public void UnrealizedDollarResultIsCorrectlyCalculatedAfterReversal()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 90, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 95, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });


            _repository.UpdateStats(_t, true);

            Assert.AreEqual(50, _t.ResultDollars);
            Assert.AreEqual(-25, _t.UnrealizedResultDollars);
            Assert.AreEqual(0, _t.UnrealizedResultDollarsLong);
            Assert.AreEqual(-25, _t.UnrealizedResultDollarsShort);
        }

        [Test]
        public void UnrealizedPctResultIsCorrectlyCalculated()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 95, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(0, _t.ResultPct);
            Assert.AreEqual(50.0m / 950, _t.UnrealizedResultPct);
            Assert.AreEqual(50.0m / 950, _t.UnrealizedResultPctLong);
        }

        [Test]
        public void UnrealizedPctResultIsCorrectlyCalculatedAfterReversal()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 90, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 95, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(-25.0m / (90 * 10 + 5 * 95), _t.UnrealizedResultPct);
            Assert.AreEqual(0, _t.UnrealizedResultPctLong);
            Assert.AreEqual(-25.0m / (5 * 95), _t.UnrealizedResultPctShort);
        }

        [Test]
        public void MultiPeriodCapitalUsageIsAverageOfNonzeroValues()
        {
            DateTime dt = new DateTime(2000, 1, 2);
            for (int i = 0; i < 10; i++)
            {
                var es = new EquitySummary
                {
                    Date = dt,
                    Total = 10000
                };
                _dbSetStub.Add(es);
            }

            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });
            _t.Orders.Add(new Order { Quantity = -10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 4), Currency = new Currency { Name = "USD" } });
            
            _t.Orders.Add(new Order { Quantity = 20, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 9, 10, 0, 0), Currency = new Currency { Name = "USD" } });
            _t.Orders.Add(new Order { Quantity = -20, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 9, 10, 0, 0), Currency = new Currency { Name = "USD" } });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual((4 * 1000 + 1 * 2000) / 5, _t.CapitalTotal);
        }

        [Test]
        public void CapitalUsageIsAverageOfPositions()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });

            var inst2 = new Instrument { ID = 2, Multiplier = 1, AssetCategory = AssetClass.Stock };
            _t.Orders.Add(new Order { Quantity = 20, Instrument = inst2, Price = 100, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(10 * 100 + 20 * 100, _t.CapitalTotal);
        }

        [Test]
        public void OpeningDateTimeIsSetToTheFirstOrder()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 90, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(2001, 1, 1), Currency = new Currency { Name = "USD" } });
            _t.Orders.Add(new Order { Quantity = -15, Instrument = _inst, Price = 95, FXRateToBase = 1, BuySell = "SELL", TradeDate = new DateTime(2000, 1, 1), Currency = new Currency { Name = "USD" } });

            _repository.UpdateStats(_t, true);

            Assert.AreEqual(new DateTime(2000, 1, 1), _t.DateOpened);
        }

        [Test]
        public void OpeningDateTimeIsSetToTheEarliestTransaction()
        {
            _t.CashTransactions.Add(new CashTransaction { Instrument = _inst, InstrumentID = 1, FXRateToBase = 1, Amount = 5, TransactionDate = new DateTime(2002, 1, 1) });

            _repository.UpdateStats(_t, true);
            
            Assert.AreEqual(new DateTime(2002, 1, 1), _t.DateOpened);
        }

        [Test]
        public void OpeningDateTimeIsSetToTheEarliestEquitySummaryDate()
        {
            _t.Orders.Add(new Order { Quantity = 10, Instrument = _inst, Price = 90, FXRateToBase = 1, BuySell = "BUY", TradeDate = new DateTime(1999, 1, 1), Currency = new Currency { Name = "USD" } });
            _t.CashTransactions.Add(new CashTransaction { Instrument = _inst, InstrumentID = 1, FXRateToBase = 1, Amount = 5, TransactionDate = new DateTime(1999, 1, 2) });

            _repository.UpdateStats(_t, true);

            //The first equity summary is at 2000,1,1. This is set in the SetUp() method above.
            Assert.AreEqual(new DateTime(2000, 1, 1), _t.DateOpened);
        }
    }
}
