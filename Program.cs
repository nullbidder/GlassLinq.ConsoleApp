using System;
using GlassLinq.Studio;
using GlassLinq.Studio.Activities;

namespace GlassLinq.Studio.Examples
{
    /// <summary>
    /// Example console application demonstrating how to use StandaloneWorkflowRunner
    /// to execute state machine XAML workflows.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("   GlassLinq Studio - Standalone Workflow Runner Demo");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Example 1: Basic execution with default condition evaluator
            Example1_BasicExecution();

            Console.WriteLine("\n\n");

            // Example 2: Execution with custom condition evaluator
            Example2_CustomConditionEvaluator();

            Console.WriteLine("\n\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Example 1: Load and run a workflow with default settings.
        /// </summary>
        static void Example1_BasicExecution()
        {
            Console.WriteLine("EXAMPLE 1: Basic Execution");
            Console.WriteLine("─────────────────────────────────────────────────────────\n");

            var runner = new StandaloneWorkflowRunner();

            // Optional: Subscribe to events for real-time monitoring
            runner.OnStateEntered += (state) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[EVENT] Entering state: {state.DisplayName}");
                Console.ResetColor();
            };

            runner.OnStateExited += (state, condition) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[EVENT] Exited state: {state.DisplayName} → Condition: {condition}");
                Console.ResetColor();
            };

            // Run the workflow
            string xamlPath = @"Workflows\Main.xaml";
            runner.Run(xamlPath);
        }

        /// <summary>
        /// Example 2: Use a custom condition evaluator to control transitions.
        /// </summary>
        static void Example2_CustomConditionEvaluator()
        {
            Console.WriteLine("EXAMPLE 2: Custom Condition Evaluator");
            Console.WriteLine("─────────────────────────────────────────────────────────\n");

            var runner = new StandaloneWorkflowRunner();

            // Set a custom condition evaluator
            // This example simulates processing 5 transactions before ending
            int transactionCount = 0;
            const int maxTransactions = 1;

            runner.SetConditionEvaluator((state, stateMachine) =>
            {
                // Handle exceptions first
                if (state.LastResult == "SystemException")
                    return "SystemException";
                if (state.LastResult == "BusinessException")
                    return "BusinessException";

                // Custom logic for transaction states
                if (state.DisplayName.Contains("GET TRANSACTION", StringComparison.OrdinalIgnoreCase))
                {
                    if (transactionCount < maxTransactions)
                    {
                        transactionCount++;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[CUSTOM EVALUATOR] Processing transaction {transactionCount}/{maxTransactions}");
                        Console.ResetColor();

                        // Set a variable in the state machine for activities to access
                        stateMachine.SetVariable("CurrentTransactionNumber", transactionCount);
                        stateMachine.SetVariable("HasMoreTransactions", transactionCount < maxTransactions);

                        return "NewTransaction";
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[CUSTOM EVALUATOR] All {maxTransactions} transactions processed");
                        Console.ResetColor();
                        return "NoMoreTransactions";
                    }
                }

                // For initialization states
                if (state.DisplayName.Contains("INITIALIZATION", StringComparison.OrdinalIgnoreCase))
                {
                    // Simulate successful initialization
                    return "Success";
                }

                // For process states
                if (state.DisplayName.Contains("PROCESS", StringComparison.OrdinalIgnoreCase))
                {
                    // Simulate random outcomes for demo purposes
                    Random random = new Random();
                    int outcome = random.Next(100);

                    if (outcome < 80) // 80% success rate
                        return "Success";
                    else if (outcome < 95) // 15% business exceptions
                        return "BusinessException";
                    else // 5% system exceptions
                        return "SystemException";
                }

                // Default behavior
                return state.LastResult ?? "Success";
            });

            // Set max iterations to prevent infinite loops
            runner.SetMaxIterations(50);

            // Run the workflow
            string xamlPath = @"Workflows\Main.xaml";
            runner.Run(xamlPath);

            Console.WriteLine($"\nTotal transactions processed: {transactionCount}");
        }

        /// <summary>
        /// Example 3: Programmatically create and execute a state machine (no XAML file).
        /// </summary>
        static void Example3_ProgrammaticExecution()
        {
            Console.WriteLine("EXAMPLE 3: Programmatic State Machine Creation");
            Console.WriteLine("─────────────────────────────────────────────────────────\n");

            // Create a simple state machine in code
            var stateMachine = new StateMachineActivity
            {
                DisplayName = "Test Workflow"
            };

            var initState = new StateActivity
            {
                DisplayName = "INITIALIZATION"
            };

            var processState = new StateActivity
            {
                DisplayName = "PROCESS"
            };

            var endState = new StateActivity
            {
                DisplayName = "END",
                IsFinalState = true
            };

            // Define transitions
            initState.Transitions.Add(new StateTransition
            {
                Condition = "Success",
                TargetState = processState
            });

            processState.Transitions.Add(new StateTransition
            {
                Condition = "Success",
                TargetState = endState
            });

            // Add states to machine
            stateMachine.States.Add(initState);
            stateMachine.States.Add(processState);
            stateMachine.States.Add(endState);
            stateMachine.InitialState = initState;

            // Execute directly (without loading from XAML)
            Console.WriteLine("Executing programmatically created state machine...\n");
            stateMachine.Execute();

            Console.WriteLine("\nProgrammatic execution completed!");
        }
    }
}