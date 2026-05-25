using System;
using System.IO;
using System.Xaml;
using GlassLinq.Studio.Activities;

namespace GlassLinq.Studio
{
    /// <summary>
    /// Standalone, lightweight execution engine for StateMachine XAML workflows.
    /// Loads a .xaml file and executes states sequentially from InitialState to completion.
    /// </summary>
    public class StandaloneWorkflowRunner
    {
        #region Delegates and Events

        /// <summary>
        /// Delegate for evaluating which transition condition to use for the current state.
        /// Return values like "Success", "NewTransaction", "NoMoreTransactions", "BusinessException", etc.
        /// </summary>
        public delegate string ConditionEvaluator(StateActivity currentState, StateMachineActivity stateMachine);

        /// <summary>
        /// Raised when a state begins execution (useful for logging/debugging).
        /// </summary>
        public event Action<StateActivity> OnStateEntered;

        /// <summary>
        /// Raised when a state completes execution.
        /// </summary>
        public event Action<StateActivity, string> OnStateExited; // state, condition result

        /// <summary>
        /// Raised for general logging messages.
        /// </summary>
        public event Action<string> OnLog;

        #endregion

        #region Private Fields

        private ConditionEvaluator _conditionEvaluator;
        private int _maxIterations = 1000;

        #endregion

        #region Constructor

        public StandaloneWorkflowRunner()
        {
            // Default condition evaluator: Uses the state's LastResult property
            _conditionEvaluator = DefaultConditionEvaluator;
        }

        #endregion

        #region Public Configuration

        /// <summary>
        /// Set a custom condition evaluator to control state transitions.
        /// </summary>
        public void SetConditionEvaluator(ConditionEvaluator evaluator)
        {
            _conditionEvaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>
        /// Set the maximum number of state transitions to prevent infinite loops.
        /// </summary>
        public void SetMaxIterations(int max)
        {
            _maxIterations = max > 0 ? max : 1000;
        }

        #endregion




        /// <summary>
        /// Execute an in-memory state machine workflow directly (used by Studio Designer).
        /// </summary>
        public void Run(StateMachineActivity stateMachine)
        {
            Log($"═══════════════════════════════════════════════════");
            Log($"WorkflowRunner: Starting IN-MEMORY execution");
            Log($"═══════════════════════════════════════════════════");

            if (!ValidateStateMachine(stateMachine))
            {
                return;
            }

            Log($"\nStarting execution from InitialState: {stateMachine.InitialState.DisplayName}");
            Log($"─────────────────────────────────────────────────");

            // Re-use your existing core execution logic
            ExecuteStateMachine(stateMachine);

            Log($"─────────────────────────────────────────────────");
            Log($"WorkflowRunner: Execution completed successfully");
            Log($"═══════════════════════════════════════════════════\n");
        }





        #region Main Execution Method

        /// <summary>
        /// Load and execute a state machine workflow from a XAML file.
        /// </summary>
        /// <param name="xamlFilePath">Path to the .xaml file containing a StateMachineActivity root.</param>
        public void Run(string xamlFilePath)
        {
            Log($"═══════════════════════════════════════════════════");
            Log($"WorkflowRunner: Starting execution");
            Log($"File: {xamlFilePath}");
            Log($"═══════════════════════════════════════════════════");

            // 1. Load the workflow from XAML
            StateMachineActivity stateMachine = LoadWorkflow(xamlFilePath);
            if (stateMachine == null)
            {
                LogError("Failed to load workflow or root element is not a StateMachineActivity");
                return;
            }

            // 2. Validate the state machine
            if (!ValidateStateMachine(stateMachine))
            {
                return;
            }

            // 3. Execute the state machine
            Log($"\nStarting execution from InitialState: {stateMachine.InitialState.DisplayName}");
            Log($"─────────────────────────────────────────────────");

            ExecuteStateMachine(stateMachine);

            Log($"─────────────────────────────────────────────────");
            Log($"WorkflowRunner: Execution completed successfully");
            Log($"═══════════════════════════════════════════════════\n");
        }

        #endregion

        #region Workflow Loading

        /// <summary>
        /// Load a StateMachineActivity from a XAML file using standard deserialization.
        /// </summary>
        private StateMachineActivity LoadWorkflow(string xamlFilePath)
        {
            if (!File.Exists(xamlFilePath))
            {
                LogError($"File not found: {xamlFilePath}");
                return null;
            }

            try
            {
                using (var stream = new FileStream(xamlFilePath, FileMode.Open, FileAccess.Read))
                {
                    object rootObject = XamlServices.Load(stream);

                    if (rootObject is StateMachineActivity stateMachine)
                    {
                        Log($"✓ Workflow loaded successfully");
                        Log($"  DisplayName: {stateMachine.DisplayName}");
                        Log($"  States: {stateMachine.States.Count}");
                        return stateMachine;
                    }
                    else
                    {
                        LogError($"Root element is not a StateMachineActivity (found: {rootObject?.GetType().Name ?? "null"})");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"XAML deserialization failed: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate that the state machine is properly configured.
        /// </summary>
        private bool ValidateStateMachine(StateMachineActivity stateMachine)
        {
            if (stateMachine.InitialState == null)
            {
                LogError("StateMachine has no InitialState defined");
                return false;
            }

            if (stateMachine.States == null || stateMachine.States.Count == 0)
            {
                LogError("StateMachine has no states defined");
                return false;
            }

            Log($"✓ StateMachine validation passed");
            return true;
        }

        #endregion

        #region State Machine Execution

        /// <summary>
        /// Execute the state machine loop: traverse states based on transition conditions.
        /// </summary>
        private void ExecuteStateMachine(StateMachineActivity stateMachine)
        {
            StateActivity currentState = stateMachine.InitialState;
            int iteration = 0;

            while (currentState != null && iteration < _maxIterations)
            {
                iteration++;

                Log($"\n[{iteration}] State: {currentState.DisplayName}");
                OnStateEntered?.Invoke(currentState);

                // Execute the state (Entry actions → Main activities → Exit actions)
                string transitionCondition = ExecuteState(currentState, stateMachine);

                OnStateExited?.Invoke(currentState, transitionCondition);

                // Check if we've reached a final state
                if (currentState.IsFinalState)
                {
                    Log($"    ► Final state reached");
                    break;
                }

                // Determine the next state based on the transition condition
                StateActivity nextState = GetNextState(currentState, transitionCondition);

                if (nextState == null)
                {
                    Log($"    ► No valid transition for condition '{transitionCondition}' - execution ending");
                    break;
                }

                Log($"    ► Transition: '{transitionCondition}' → {nextState.DisplayName}");
                currentState = nextState;
            }

            if (iteration >= _maxIterations)
            {
                LogWarning($"Maximum iterations ({_maxIterations}) reached - possible infinite loop detected");
            }
        }

        #endregion

        #region State Execution

        /// <summary>
        /// Execute a single state: Entry actions → Main activities → Exit actions.
        /// Returns the transition condition to use for moving to the next state.
        /// </summary>
        private string ExecuteState(StateActivity state, StateMachineActivity stateMachine)
        {
            try
            {
                // 1. Execute Entry Actions
                if (state.EntryActions != null && state.EntryActions.Count > 0)
                {
                    Log($"    ├─ Entry Actions ({state.EntryActions.Count}):");
                    foreach (var action in state.EntryActions)
                    {
                        try
                        {
                            Log($"    │  ├─ {action.DisplayName}");
                            action.Execute();
                        }
                        catch (Exception ex)
                        {
                            LogError($"       │  └─ ERROR: {ex.Message}");
                        }
                    }
                }

                // 2. Execute Main Activities
                if (state.Activities != null && state.Activities.Count > 0)
                {
                    // Filter out identical object reference pointers injected by duplicate XAML tags
                    var distinctActivities = state.Activities.Where(a => a != null).Distinct().ToList();

                    if (distinctActivities.Count > 0)
                    {
                        Log($"    ├─ Main Activities ({distinctActivities.Count} unique top-level node(s)):");
                        foreach (var activity in distinctActivities)
                        {
                            try
                            {
                                Log($"    │  ├─ Executing Node: {activity.DisplayName}");

                                // Execute the sequence/activity container once. 
                                // Because it's a Sequence or linked activity, it cascades natively.
                                activity.Execute();
                            }
                            catch (BusinessRuleException brex)
                            {
                                Log($"    │  └─ Business Exception Captured: {brex.Message}");
                                state.LastResult = "BusinessException";
                            }
                            catch (Exception ex)
                            {
                                LogError($"       │  └─ System Exception Captured: {ex.Message}");
                                state.LastResult = "SystemException";
                            }
                        }
                    }
                    else
                    {
                        InvokeStateFallback(state);
                    }
                }
                else
                {
                    InvokeStateFallback(state);
                }

                // 3. Execute Exit Actions
                if (state.ExitActions != null && state.ExitActions.Count > 0)
                {
                    Log($"    └─ Exit Actions ({state.ExitActions.Count}):");
                    foreach (var action in state.ExitActions)
                    {
                        try
                        {
                            Log($"       ├─ {action.DisplayName}");
                            action.Execute();
                        }
                        catch (Exception ex)
                        {
                            LogError($"       └─ ERROR: {ex.Message}");
                        }
                    }
                }

                // 4. Evaluate which transition condition to use
                string condition = _conditionEvaluator(state, stateMachine);
                Log($"    └─ Condition Result: '{condition}'");

                return condition;
            }
            catch (Exception ex)
            {
                LogError($"    └─ CRITICAL ERROR in state execution: {ex.Message}");
                return "SystemException";
            }
        }

        /// <summary>
        /// Safe execution fallback if the explicit activity collections are empty.
        /// </summary>
        private void InvokeStateFallback(StateActivity state)
        {
            try
            {
                Log($"    │  ► Invoking State Base Container Direct Execution...");
                // Call the base sequence execution wrapper directly
                state.Execute();
            }
            catch (BusinessRuleException brex)
            {
                Log($"    ├─ Business Exception: {brex.Message}");
                state.LastResult = "BusinessException";
            }
            catch (Exception ex)
            {
                LogError($"    ├─ System Exception: {ex.Message}");
                state.LastResult = "SystemException";
            }
        }
        #endregion

        #region Transition Logic

        /// <summary>
        /// Find the next state based on the current state's transitions and the condition.
        /// </summary>
        private StateActivity GetNextState(StateActivity currentState, string condition)
        {
            if (currentState.Transitions == null || currentState.Transitions.Count == 0)
            {
                return null;
            }

            // Use the state's built-in GetNextState method
            StateActivity nextState = currentState.GetNextState(condition);

            // If no match found, try a "Default" transition as fallback
            if (nextState == null)
            {
                nextState = currentState.GetNextState("Default");
            }

            return nextState;
        }

        #endregion

        #region Default Condition Evaluator

        /// <summary>
        /// Default logic for determining the transition condition.
        /// Uses the state's LastResult property, with special handling for transaction states.
        /// </summary>
        private string DefaultConditionEvaluator(StateActivity state, StateMachineActivity stateMachine)
        {
            // 1. Check for exceptions first (highest priority)
            if (state.LastResult == "SystemException")
                return "SystemException";

            if (state.LastResult == "BusinessException")
                return "BusinessException";

            // 2. Special handling for "Get Transaction" states
            if (state.DisplayName.Contains("GET TRANSACTION", StringComparison.OrdinalIgnoreCase) ||
                state.DisplayName.Contains("Get Transaction", StringComparison.OrdinalIgnoreCase))
            {
                // Check if the state machine has a variable indicating transaction availability
                if (stateMachine.Variables != null)
                {
                    if (stateMachine.Variables.ContainsKey("HasMoreTransactions"))
                    {
                        bool hasMore = (bool)stateMachine.Variables["HasMoreTransactions"];
                        return hasMore ? "NewTransaction" : "NoMoreTransactions";
                    }

                    // Alternative: Check for a transaction counter
                    if (stateMachine.Variables.ContainsKey("TransactionCounter") &&
                        stateMachine.Variables.ContainsKey("MaxTransactions"))
                    {
                        int counter = (int)stateMachine.Variables["TransactionCounter"];
                        int max = (int)stateMachine.Variables["MaxTransactions"];
                        return counter < max ? "NewTransaction" : "NoMoreTransactions";
                    }
                }

                // Default for transaction states: assume no more transactions
                return "NoMoreTransactions";
            }

            // 3. For all other states, use the LastResult (defaults to "Success")
            return state.LastResult ?? "Success";
        }

        #endregion

        #region Logging Helpers

        private void Log(string message)
        {
            Console.WriteLine(message);
            OnLog?.Invoke(message);
        }

        private void LogError(string message)
        {
            string errorMsg = $"ERROR: {message}";
            Console.WriteLine(errorMsg);
            OnLog?.Invoke(errorMsg);
        }

        private void LogWarning(string message)
        {
            string warningMsg = $"WARNING: {message}";
            Console.WriteLine(warningMsg);
            OnLog?.Invoke(warningMsg);
        }

        #endregion
    }
}