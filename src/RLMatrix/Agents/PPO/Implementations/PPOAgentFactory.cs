﻿using OneOf;
using RLMatrix.Agents.DQN.Domain;
using TorchSharp;
using static TorchSharp.torch;

namespace RLMatrix.Agents.PPO.Implementations
{
    public static class PPOAgentFactory<T>
        {
            public static IDiscretePPOAgent<T> ComposeDiscretePPOAgent(PPOAgentOptions options, int[] ActionSizes, OneOf<int, (int, int)> 
                StateSizes, IPPONetProvider<T> netProvider = null, IGAIL<T> gail = null)
            {
                netProvider ??= new PPONetProviderBase<T>(options.Width, options.Depth, options.UseRNN);
                
                var device = torch.device(torch.cuda.is_available() ? "cuda" : "cpu");
                var envSizeDTO = new DiscreteEnvSizeDTO { actionSize = ActionSizes, stateSize = StateSizes };
                var actorNet = netProvider.CreateActorNet(envSizeDTO).to(device);
                var criticNet = netProvider.CreateCriticNet(envSizeDTO).to(device);
                var actorOptimizer = optim.Adam(actorNet.parameters(), lr: options.LR, amsgrad: true);
                var criticOptimizer = optim.Adam(criticNet.parameters(), lr: options.LR, amsgrad: true);
              
                var actorLrScheduler = new optim.lr_scheduler.impl.CyclicLR(actorOptimizer, options.LR * 0.5f, options.LR * 2f, step_size_up: 500, step_size_down: 2000, cycle_momentum: false);
                var criticlLrScheduler = new optim.lr_scheduler.impl.CyclicLR(criticOptimizer, options.LR * 0.5f, options.LR * 2f, step_size_up: 500, step_size_down: 2000, cycle_momentum: false);


                var PPOOptimize = new PPOOptimize<T>(actorNet, criticNet, actorOptimizer, criticOptimizer, options, device, ActionSizes, new (float, float)[0], actorLrScheduler, criticlLrScheduler, gail);


                if(options.UseRNN)
                {
                    var Agent = new DiscreteRecurrentPPOAgent<T>
                    {
                        actorNet = actorNet,
                        criticNet = criticNet,
                        Optimizer = PPOOptimize,
                        Memory = new ReplayMemory<T>(options.MemorySize),
                        ActionSizes = ActionSizes,
                        Options = options,
                        Device = device,
                    };
                    return Agent;
                }
                else
                {
                    var Agent = new DiscretePPOAgent<T>
                    {
                        actorNet = actorNet,
                        criticNet = criticNet,
                        Optimizer = PPOOptimize,
                        Memory = new ReplayMemory<T>(options.MemorySize),
                        ActionSizes = ActionSizes,
                        Options = options,
                        Device = device,
                    };

                    return Agent;
                }

            }



        }
}


    

