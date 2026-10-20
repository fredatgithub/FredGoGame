using Stone = FredGoGame.Enumerations.Stone;

namespace FredGoGame
{
  internal sealed class GameEngine
  {
    private readonly int _size;
    private readonly Stone[,] _board;
    private readonly HashSet<string> _positions = [];
    private static Stone Opposite(Stone color) => color == Stone.Black ? Stone.White : Stone.Black;

    public GameEngine(int size)
    {
      _size = size;
      _board = new Stone[size, size];
      _positions.Add(GetPositionKey());
    }

    private GameEngine(GameEngine source)
    {
      _size = source._size;
      _board = (Stone[,])source._board.Clone();
      _positions = new HashSet<string>(source._positions);
    }

    public Stone At(int x, int y) => _board[x, y];

    public bool TryPlay(int x, int y, Stone color, out int captured)
    {
      captured = 0;
      if (color == Stone.Empty || x < 0 || x >= _size || y < 0 || y >= _size ||
          _board[x, y] != Stone.Empty)
      {
        return false;
      }

      var candidate = new GameEngine(this);
      candidate._board[x, y] = color;
      Stone opponent = Opposite(color);
      var examined = new HashSet<(int X, int Y)>();

      foreach ((int nx, int ny) in candidate.GetNeighbors(x, y))
      {
        if (candidate._board[nx, ny] != opponent || examined.Contains((nx, ny)))
        {
          continue;
        }

        (List<(int X, int Y)> stones, int liberties) = candidate.GetGroup(nx, ny);
        examined.UnionWith(stones);
        if (liberties == 0)
        {
          foreach ((int stoneX, int stoneY) in stones)
          {
            candidate._board[stoneX, stoneY] = Stone.Empty;
            captured++;
          }
        }
      }

      if (candidate.GetGroup(x, y).Liberties == 0)
      {
        return false;
      }

      string key = candidate.GetPositionKey();
      if (_positions.Contains(key))
      {
        return false;
      }

      Array.Copy(candidate._board, _board, candidate._board.Length);
      _positions.Add(key);
      return true;
    }

    public (int X, int Y)? FindComputerMove()
    {
      (int X, int Y)? bestMove = null;
      double bestScore = double.NegativeInfinity;
      bool boardIsEmpty = true;
      for (int y = 0; y < _size && boardIsEmpty; y++)
      {
        for (int x = 0; x < _size; x++)
        {
          if (_board[x, y] != Stone.Empty)
          {
            boardIsEmpty = false;
            break;
          }
        }
      }

      for (int y = 0; y < _size; y++)
      {
        for (int x = 0; x < _size; x++)
        {
          if (_board[x, y] != Stone.Empty)
          {
            continue;
          }

          var candidate = new GameEngine(this);
          if (!candidate.TryPlay(x, y, Stone.White, out int captured))
          {
            continue;
          }

          double score = EvaluateMove(x, y, captured, candidate, boardIsEmpty);
          if (score > bestScore)
          {
            bestScore = score;
            bestMove = (x, y);
          }
        }
      }

      return bestScore < 0.9 ? null : bestMove;
    }

    public (double Black, double White) CalculateScore(double komi)
    {
      double black = 0;
      double white = komi;
      var visited = new bool[_size, _size];

      for (int y = 0; y < _size; y++)
      {
        for (int x = 0; x < _size; x++)
        {
          if (_board[x, y] == Stone.Black)
          {
            black++;
          }
          else if (_board[x, y] == Stone.White)
          {
            white++;
          }
          else if (!visited[x, y])
          {
            List<(int X, int Y)> region = GetEmptyRegion(x, y, visited);
            var borders = new HashSet<Stone>();
            foreach ((int regionX, int regionY) in region)
            {
              foreach ((int nx, int ny) in GetNeighbors(regionX, regionY))
              {
                if (_board[nx, ny] != Stone.Empty)
                {
                  borders.Add(_board[nx, ny]);
                }
              }
            }

            if (borders.Count == 1 && borders.Contains(Stone.Black))
            {
              black += region.Count;
            }
            else if (borders.Count == 1 && borders.Contains(Stone.White))
            {
              white += region.Count;
            }
          }
        }
      }

      return (black, white);
    }

    private double EvaluateMove(int x, int y, int captured, GameEngine candidate, bool boardIsEmpty)
    {
      double score = 1.2 + captured * 12;
      int liberties = candidate.GetGroup(x, y).Liberties;
      score += Math.Min(liberties, 5) * 0.35;

      foreach ((int nx, int ny) in GetNeighbors(x, y))
      {
        if (_board[nx, ny] == Stone.White)
        {
          score += 1.8;
        }
        else if (_board[nx, ny] == Stone.Black)
        {
          score += 1.6;
          if (GetGroup(nx, ny).Liberties == 1)
          {
            score += 4;
          }
        }
      }

      int nearestWhite = _size;
      int nearestBlack = _size;
      for (int boardY = 0; boardY < _size; boardY++)
      {
        for (int boardX = 0; boardX < _size; boardX++)
        {
          int distance = Math.Abs(x - boardX) + Math.Abs(y - boardY);
          if (_board[boardX, boardY] == Stone.White)
          {
            nearestWhite = Math.Min(nearestWhite, distance);
          }
          else if (_board[boardX, boardY] == Stone.Black)
          {
            nearestBlack = Math.Min(nearestBlack, distance);
          }
        }
      }

      if (boardIsEmpty)
      {
        double center = (_size - 1) / 2.0;
        score += 3.5 - (Math.Abs(x - center) + Math.Abs(y - center)) * 0.25;
      }
      else
      {
        if (nearestWhite < _size)
        {
          score += Math.Max(0, 3.2 - nearestWhite * 0.45);
        }
        if (nearestBlack < _size)
        {
          score += Math.Max(0, 2.2 - nearestBlack * 0.35);
        }
        score += Math.Min(liberties, 4) * 0.1;
      }

      return score;
    }

    private (List<(int X, int Y)> Stones, int Liberties) GetGroup(int startX, int startY)
    {
      Stone color = _board[startX, startY];
      var stones = new List<(int X, int Y)>();
      var visited = new HashSet<(int X, int Y)>();
      var liberties = new HashSet<(int X, int Y)>();
      var pending = new Stack<(int X, int Y)>();
      pending.Push((startX, startY));

      while (pending.Count > 0)
      {
        (int x, int y) = pending.Pop();
        if (!visited.Add((x, y)))
        {
          continue;
        }

        stones.Add((x, y));
        foreach ((int nx, int ny) in GetNeighbors(x, y))
        {
          if (_board[nx, ny] == Stone.Empty)
          {
            liberties.Add((nx, ny));
          }
          else if (_board[nx, ny] == color && !visited.Contains((nx, ny)))
          {
            pending.Push((nx, ny));
          }
        }
      }

      return (stones, liberties.Count);
    }

    private List<(int X, int Y)> GetEmptyRegion(int startX, int startY, bool[,] visited)
    {
      var region = new List<(int X, int Y)>();
      var pending = new Stack<(int X, int Y)>();
      pending.Push((startX, startY));
      visited[startX, startY] = true;

      while (pending.Count > 0)
      {
        (int x, int y) = pending.Pop();
        region.Add((x, y));
        foreach ((int nx, int ny) in GetNeighbors(x, y))
        {
          if (!visited[nx, ny] && _board[nx, ny] == Stone.Empty)
          {
            visited[nx, ny] = true;
            pending.Push((nx, ny));
          }
        }
      }

      return region;
    }

    private IEnumerable<(int X, int Y)> GetNeighbors(int x, int y)
    {
      if (x > 0) yield return (x - 1, y);
      if (x + 1 < _size) yield return (x + 1, y);
      if (y > 0) yield return (x, y - 1);
      if (y + 1 < _size) yield return (x, y + 1);
    }

    private string GetPositionKey()
    {
      var key = new char[_size * _size];
      for (int y = 0; y < _size; y++)
      {
        for (int x = 0; x < _size; x++)
        {
          key[y * _size + x] = (char)('0' + (byte)_board[x, y]);
        }
      }

      return new string(key);
    }
  }
}
