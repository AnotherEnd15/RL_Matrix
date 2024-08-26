using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using RLMatrix;
using RLMatrix.Agents.Common;
using System.Threading.Tasks;

public partial class BallBalanceTrainingManager : Node
{
    [Export] private int poolingRate = 5;
    [Export] private float timeScale = 1f;
    [Export] private float stepInterval = 0.02f;

    private PPOAgentOptions optsppo = new PPOAgentOptions(
        batchSize: 64,
        memorySize: 10000,
        gamma: 0.99f,
        gaeLambda: 0.95f,
        lr: 3e-4f,
        width: 128,
        depth: 2,
        clipEpsilon: 0.2f,
        vClipRange: 0.2f,
        cValue: 0.5f,
        ppoEpochs: 3,
        clipGradNorm: 0.5f,
        entropyCoefficient: 0.005f,
        useRNN: false
    );

    private LocalContinuousRolloutAgent<float[]> myAgent;
    private List<BallBalanceEnv> myEnvs;
    private int stepCounter = 0;
    private float accumulatedTime = 0f;
    private int stepsTooSlowInRow = 0;

    public override void _Ready()
    {
        Engine.TimeScale = timeScale;
        myEnvs = GetAllChildrenOfType<BallBalanceEnv>(this).ToList();

        if (myEnvs.Count == 0)
        {
            GD.PrintErr("No BallBalanceEnv nodes found in children.");
            return;
        }

        InitializeEnvironments();

        GD.Print($"Found {myEnvs.Count} environments with pooling rate {poolingRate}.");

        _ = InitializeAgent();
    }

    private List<T> GetAllChildrenOfType<T>(Node parentNode) where T : class
    {
        List<T> resultList = new List<T>();
        AddChildrenOfType(parentNode, resultList);
        return resultList;
    }

    private void AddChildrenOfType<T>(Node node, List<T> resultList) where T : class
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T typedChild)
            {
                resultList.Add(typedChild);
            }
            AddChildrenOfType(child, resultList);
        }
    }

    private void InitializeEnvironments()
    {
        foreach (var env in myEnvs)
        {
            env.Initialize(poolingRate);
        }
    }

    private async Task InitializeAgent()
    {
        await Task.Run(() =>
        {
            myAgent = new LocalContinuousRolloutAgent<float[]>(optsppo, myEnvs);
        });

        Engine.TimeScale = timeScale;
    }

    public override void _Process(double delta)
    {
        if (myAgent == null) return;

        accumulatedTime += (float)delta;

        while (accumulatedTime >= stepInterval / Engine.TimeScale)
        {
            /*
            if(accumulatedTime > 2 * stepInterval)
            {
                stepsTooSlowInRow++;

                if(stepsTooSlowInRow > 10)
                {
                    GD.PrintErr("Too slow, throttling.");
                    stepsTooSlowInRow = 0;
                    timeScale *= 0.9f;
                    Engine.TimeScale = timeScale;
                }
            }
            else
            {
                stepsTooSlowInRow = 0;
            }
            */
            
            PerformStep();
            accumulatedTime -= stepInterval / (float)Engine.TimeScale;
        }
    }

    private void PerformStep()
    {
        if (stepCounter % poolingRate == poolingRate - 1)
        {
            // Actual agent-env step
            myAgent.StepSync(true);
        }
        else
        {
            // Ghost step
            foreach (var env in myEnvs)
            {
                env.GhostStep();
            }
        }
        stepCounter = (stepCounter + 1) % poolingRate;
    }
}