# Traceability matrix

> Maps every specification Rule ID to its source section, production file, verification method, and current status.
> Statuses: Proposed · Approved · Implemented · Verified · Blocked.

| Rule ID | Specification section | Source section | Production file/symbol | Verification | Status |
|---|---|---|---|---|---|
| A.1 | SYMBOL_DISABLED | §2 | Domain/Market/SymbolInfo.cs | Unit test | Proposed |
| B.1 | InternalTimeUTC | §3.1 | Infrastructure/Clock | Unit test | Proposed |
| B.2 | EntryWindow | §3.2 | Domain/TradingWindow | Unit test | Proposed |
| B.3 | FridayCutoff | §3.3 | Domain/TradingWindow | Unit test | Proposed |
| B.4 | FridayCloseAll | §3.3 | Domain/TradingWindow | Unit test | Proposed |
| B.5 | RolloverBlackout | §3.3 | Domain/TradingWindow | Unit test | Proposed |
| C.1 | DataQualityGate | §4 | Domain/DataValidator | Unit test | Proposed |
| C.2 | WarmupMinimum | §4 | Domain/DataValidator | Unit test | Proposed |
| D.1 | ClosePriceBasis | §5 | Domain/Indicators | Unit test | Proposed |
| D.2 | H1_Indicators | §5 | Domain/Indicators | Unit test | Proposed |
| D.3 | M15_Indicators | §5 | Domain/Indicators | Unit test | Proposed |
| E.1 | TrendBuy | §6.1 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| E.2 | TrendSell | §6.2 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| E.3 | TrendNeutral | §6 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| F.1 | VolatilityRatio | §7 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| F.2 | VolatilityBand | §7 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| G.1 | SignalDefinitions | §8 | Domain/Market/Candle.cs | Unit test | Approved |
| G.2 | BuySignal | §8.1 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| G.3 | SellSignal | §8.2 | Domain/Signals/SignalEvaluator.cs | Unit test | Approved |
| H.1 | VolumeBaseline | §9 | Domain/Risk/VolumeFilter.cs | Unit test | Approved |
| H.2 | VolumeFilter | §9 | Domain/Risk/VolumeFilter.cs | Unit test | Approved |
| I.1 | SpreadBaseline | §10 | Domain/Risk/SpreadFilter.cs | Unit test | Approved |
| I.2 | SpreadFilter | §10 | Domain/Risk/SpreadFilter.cs | Unit test | Approved |
| I.3 | SpreadTriggerCancel | §10 | Domain/Risk/SpreadFilter.cs | Unit test | Approved |
| J.1 | ConfirmedSwingLow | §11.1 | Domain/Risk/SwingDetector.cs | Unit test | Approved |
| J.2 | ConfirmedSwingHigh | §11.1 | Domain/Risk/SwingDetector.cs | Unit test | Approved |
| J.3 | SelectSwing | §11.2 | Domain/Risk/SwingDetector.cs | Unit test | Approved |
| J.4 | StopLossLevel | §11.3 | Domain/Risk/StopLossCalculator.cs | Unit test | Approved |
| J.5 | StopDistanceBounds | §11.3 | Domain/Risk/StopLossCalculator.cs | Unit test | Approved |
| J.6 | StopBrokerLimits | §11.3 | Domain/Risk/StopLossCalculator.cs | Unit test | Approved |
| K.1 | EntryPrice | §12.1 | Domain/Orders/OrderEntryCalculator.cs | Unit test | Approved |
| K.2 | OrderExpiry | §12.2 | Domain/Orders/OrderEntryCalculator.cs | Unit test | Approved |
| K.3 | PreTriggerValidation | §12.3 | Blocked — needs ExecutionPort | Unit test | Approved |
| K.4 | TriggerDistanceCheck | §12.3 | Domain/Orders/OrderEntryCalculator.cs | Unit test | Approved |
| K.5 | SlippageControl | §12.4 | Blocked — needs ExecutionPort | Unit test | Approved |
| L.1 | TradeRiskMoney | §13 | Domain/Risk/PositionSizer.cs | Unit test | Approved |
| L.2 | RawVolume | §13 | Domain/Risk/PositionSizer.cs | Unit test | Approved |
| L.3 | VolumeRounding | §13 | Domain/Risk/PositionSizer.cs | Unit test | Approved |
| L.4 | MarginCheck | §13 | Domain/Risk/PositionSizer.cs | Unit test | Approved |
| L.5 | PostRoundingRisk | §13 | Domain/Risk/PositionSizer.cs | Unit test | Approved |
| M.1 | TakeProfit | §14.1 | Domain/Orders/TradeManagementCalculator.cs | Unit test | Approved |
| M.2 | BreakEvenDisabled | §14.2 | No code (flag/constraint) | Audit | Approved |
| M.3 | TimeExit | §14.3 | Domain/Orders/TradeManagementCalculator.cs | Unit test | Approved |
| M.4 | OppositeSignalNoClose | §14.4 | No code (constraint) | Audit | Approved |
| N.1 | NewsCalendarSource | §15.1 | Blocked — needs NewsCalendarPort | Unit test | Approved |
| N.2 | LevelANews | §15.2 | Domain/Orders/NewsWindowCalculator.cs | Unit test | Approved |
| N.3 | NewsProhibitedWindow | §15.3 | Domain/Orders/NewsWindowCalculator.cs | Unit test | Approved |
| N.4 | PendingCancelPreNews | §15.3 | Blocked — needs ExecutionPort | Unit test | Approved |
| N.5 | ClosePreNews | §15.3 | Blocked — needs ExecutionPort | Unit test | Approved |
| O.1 | RiskPerTrade | §16.1 | Domain/Risk/PortfolioRiskGuard.cs | Unit test | Approved |
| O.2 | MaxReservedRisk | §16.1 | Domain/Risk/PortfolioRiskGuard.cs | Unit test | Approved |
| O.3 | MetalBasket | §16.2 | Domain/Risk/PortfolioRiskGuard.cs | Unit test | Approved |
| O.4 | USDDirectionalExposure | §16.3 | Domain/Risk/PortfolioRiskGuard.cs | Unit test | Approved |
| P.1 | DayDefinition | §17.1 | Domain/Risk/DrawdownGuard.cs | Unit test | Approved |
| P.2 | DailyDrawdown | §17 | Domain/Risk/DrawdownGuard.cs | Unit test | Approved |
| P.3 | WeeklyDrawdown | §17 | Domain/Risk/DrawdownGuard.cs | Unit test | Approved |
| P.4 | MaxDailyEntries | §17 | Domain/Risk/DrawdownGuard.cs | Unit test | Approved |
| P.5 | ConsecutiveLoss | §17.2 | Domain/Risk/DrawdownGuard.cs | Unit test | Approved |
| P.6 | KillSwitch | §17.3 | Domain/Risk/DrawdownGuard.cs | Unit test | Approved |
| Q.1 | DisconnectBehavior | §18 | Blocked — needs ConnectionPort | Unit test | Approved |
| Q.2 | StateMismatch | §18 | Blocked — needs ConnectionPort | Unit test | Approved |
| R.1 | States | §19 | Domain/Market/BotState.cs | Unit test | Proposed |
| R.2 | Transitions | §19 | Domain/StateMachine | Unit test | Proposed |
| R.3 | SinglePositionPerSymbol | §19 | Domain/StateMachine | Unit test | Proposed |
| S.1 | CandleCloseSequence | §20 | Application/Orchestration | Integration test | Proposed |
| S.2 | ReasonCodes | §20 | Domain/Market/ReasonCode.cs | Unit test | Proposed |
| T.1 | BacktestDataRequirements | §21.1 | Backtest/ | Integration test | Proposed |
| T.2 | BacktestCosts | §21.2 | Backtest/ | Integration test | Proposed |
| T.3 | SLTPConflictResolution | §21.3 | Backtest/ | Unit test | Proposed |
| U.1 | WalkForwardProtocol | §22 | Backtest/WalkForward | Integration test | Proposed |
| V.1 | MonteCarloProtocol | §23 | Backtest/MonteCarlo | Integration test | Proposed |
| V.2 | MonteCarloOutputs | §23 | Backtest/MonteCarlo | Unit test | Proposed |
| W.1 | AcceptanceTable | §24 | Backtest/Reports | Integration test | Proposed |
| W.2 | SharpeDefinition | §24 | Backtest/Reports | Unit test | Proposed |
| W.3 | RobustnessChecks | §24 | Backtest/Reports | Integration test | Proposed |
| X.1 | AblationComparisons | §25 | Backtest/Ablation | Integration test | Proposed |
| Y.1 | PilotPhases | §26 | N/A (process) | Checklist | Proposed |
| Y.2 | NoRiskIncreaseOnProfit | §26 | N/A (process) | Checklist | Proposed |
| Z.1 | PerCandleLog | §27 | Domain/Logger | Unit test | Proposed |
| Z.2 | WeeklyReport | §27 | Domain/Logger | Integration test | Proposed |
| AA.1 | FixedParameters | §29 | N/A (classification) | Audit | Proposed |
| AA.2 | ResearchParameters | §29 | N/A (classification) | Audit | Proposed |
