using System;
using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Command;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        protected readonly List<IBoardCommand> _commands = new();

        protected void UpdateCommands()
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                var cmd = _commands[i];

                if (cmd == null)
                {
                    _commands.RemoveAt(i);
                    i--;
                }
                else if (cmd.IsCompleted)
                {
                    cmd.Dispose();
                    _commands.RemoveAt(i);
                    i--;
                }
                else if (!cmd.IsRunning)
                {
                    try
                    {
                        cmd.Run();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                        cmd.Dispose();
                        _commands.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        protected void ClearAllCommands()
        {
            foreach (var command in _commands)
            {
                if (command == null)
                {
                    continue;
                }

                command.Dispose();
            }

            _commands.Clear();
        }
    }
}
