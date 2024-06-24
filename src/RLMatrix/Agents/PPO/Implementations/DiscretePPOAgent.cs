﻿using RLMatrix.Agents.Common;
using RLMatrix.Memories;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.optim;
using static TorchSharp.torch.optim.lr_scheduler;

namespace RLMatrix.Agents.PPO.Implementations
{
    public class DiscretePPOAgent<T> : IDiscretePPOAgent<T>
    {
        //Todo: these PPOActor/Critic should be interfaces
        public required PPOActorNet actorNet { get; set; }
        public required PPOCriticNet criticNet { get; set; }
        public required IOptimize<T> Optimizer { get; init; }
        public required IMemory<T> Memory { get; set; }
        public required int[] ActionSizes { get; init; }
        public required PPOAgentOptions Options { get; init; }
        public required Device Device { get; init; }

        public void AddTransition(IEnumerable<TransitionPortable<T>> transitions)
        {
            Memory.Push(transitions.ToTransitionInMemory());
        }

        public void OptimizeModel()
        {
            Optimizer.Optimize(Memory);
        }

        public int[][] SelectActions(T[] states, bool isTraining)
        {
            using (var scope = torch.no_grad())
            {
                Tensor stateTensor = Utilities<T>.StateBatchToTensor(states, Device);
                var result = actorNet.forward(stateTensor);
                int[][] actions = new int[states.Length][];

                if (isTraining)
                {
                    // Discrete Actions
                    for (int i = 0; i < states.Length; i++)
                    {
                        actions[i] = new int[ActionSizes.Length];
                        for (int j = 0; j < ActionSizes.Length; j++)
                        {
                            var actionProbs = result[i, j];
                            var actionSample = torch.multinomial(actionProbs, 1, true);
                            actions[i][j] = (int)actionSample.item<long>();
                        }
                    }
                }
                else
                {
                    // Discrete Actions
                    for (int i = 0; i < states.Length; i++)
                    {
                        actions[i] = new int[ActionSizes.Length];
                        for (int j = 0; j < ActionSizes.Length; j++)
                        {
                            var actionProbs = result[i, j];
                            var actionIndex = actionProbs.argmax();
                            actions[i][j] = (int)actionIndex.item<long>();
                        }
                    }
                }

                return actions;
            }
        }


        int[][] SelectActions2(T[] states, bool isTraining)
        {
            int[][] actions = new int[states.Length][];
            float[][] continuousActions = new float[states.Length][];

            for (int i = 0; i < states.Length; i++)
            {
                using (var scope = torch.no_grad())
                {
                    Tensor stateTensor = Utilities<T>.StateToTensor(states[i], Device);
                    var result = actorNet.forward(stateTensor);

                    if (isTraining)
                    {
                        // Discrete Actions
                        actions[i] = PPOActionSelection<T>.SelectDiscreteActionsFromProbs(result, ActionSizes);
                        // Continuous Actions
                        continuousActions[i] = PPOActionSelection<T>.SampleContinuousActions(result, ActionSizes, new (float, float)[0]);
                    }
                    else
                    {
                        // Discrete Actions
                        actions[i] = PPOActionSelection<T>.SelectGreedyDiscreteActions(result, ActionSizes);
                        // Continuous Actions
                        continuousActions[i] = PPOActionSelection<T>.SelectMeanContinuousActions(result, ActionSizes, new (float, float)[0]);
                    }
                }
            }

            return actions;
        }

        public virtual (int[] actions, Tensor? memoryState, Tensor? memoryState2)[] SelectActionsRecurrent((T state, Tensor? memoryState, Tensor? memoryState2)[] states, bool isTraining)
        {
            throw new Exception("Using recurrent action selection with non recurrent agent, use int[][] SelectActions(T[] states, bool isTraining) signature instead");
        }

        public void Save(string path)
        {
            // Check if path ends with "/", if not, append it
            var modelPath = path.EndsWith("/") ? path : path + "/";

            // Save the policy network
            string actorNetPath = GetNextAvailableModelPath(modelPath, "modelActor");
            actorNet.save(actorNetPath);

            // Save the target network
            string criticNetPath = GetNextAvailableModelPath(modelPath, "modelCritic");
            criticNet.save(criticNetPath);
        }

        private string GetNextAvailableModelPath(string modelPath, string modelName)
        {
            // Read all files in the directory
            var files = Directory.GetFiles(modelPath);

            // Find the highest number of files with the same name
            int maxNumber = files
                .Where(file => file.Contains(modelName))
                .Select(file => Path.GetFileNameWithoutExtension(file).Split("_").LastOrDefault())
                .Where(number => int.TryParse(number, out _))
                .DefaultIfEmpty("0")
                .Max(number => int.Parse(number));

            // Append the next number to the model name
            string nextModelPath = $"{modelPath}{modelName}_{maxNumber + 1}";

            return nextModelPath;
        }

        public void Load(string path, LRScheduler scheduler = null)
        {
            // Check if path ends with "/", if not, append it
            var modelPath = path.EndsWith("/") ? path : path + "/";

            // Load the policy network
            string actorNetPath = GetLatestModelPath(modelPath, "modelActor");
            actorNet.load(actorNetPath, strict: true);

            // Load the target network
            string criticNetPath = GetLatestModelPath(modelPath, "modelCritic");
            criticNet.load(criticNetPath, strict: true);

            Optimizer.UpdateOptimizers(scheduler);
        }

        private string GetLatestModelPath(string modelPath, string modelName)
        {
            // Read all files in the directory
            var files = Directory.GetFiles(modelPath);

            // Find the highest number of files with the same name
            int maxNumber = files
                .Where(file => file.Contains(modelName))
                .Select(file => Path.GetFileNameWithoutExtension(file).Split("_").LastOrDefault())
                .Where(number => int.TryParse(number, out _))
                .DefaultIfEmpty("0")
                .Max(number => int.Parse(number));

            // Get the latest model path
            string latestModelPath = $"{modelPath}{modelName}_{maxNumber}";

            return latestModelPath;
        }
    }
}


    

