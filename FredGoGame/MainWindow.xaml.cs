using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FredGoGame
{
    public partial class MainWindow : Window
    {
        private const double BoardExtent = 600;
        private const double BoardMargin = 32;
        private const double Komi = 6.5;
        private GameEngine? _game;
        private int _boardSize = 9;
        private int _consecutivePasses;
        private int _lastX = -1;
        private int _lastY = -1;
        private bool _isThinking;
        private bool _isFinished;

        public MainWindow()
        {
            InitializeComponent();
            StartNewGame();
        }

        private void StartNewGame()
        {
            _boardSize = BoardSizeSelector.SelectedIndex switch
            {
                1 => 13,
                2 => 19,
                _ => 9
            };
            _game = new GameEngine(_boardSize);
            _consecutivePasses = 0;
            _lastX = -1;
            _lastY = -1;
            _isThinking = false;
            _isFinished = false;
            StatusText.Text = "À vous de jouer — posez une pierre noire.";
            PassButton.IsEnabled = true;
            UpdateScore();
            DrawBoard();
        }

        private void BoardSizeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_game is not null)
            {
                StartNewGame();
            }
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e) => StartNewGame();

        private async void BoardCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_game is null || _isThinking || _isFinished)
            {
                return;
            }

            Point point = e.GetPosition(BoardCanvas);
            double spacing = (BoardExtent - BoardMargin * 2) / (_boardSize - 1);
            int x = (int)Math.Round((point.X - BoardMargin) / spacing);
            int y = (int)Math.Round((point.Y - BoardMargin) / spacing);

            if (x < 0 || x >= _boardSize || y < 0 || y >= _boardSize ||
                Math.Abs(point.X - (BoardMargin + x * spacing)) > spacing * 0.48 ||
                Math.Abs(point.Y - (BoardMargin + y * spacing)) > spacing * 0.48)
            {
                return;
            }

            if (!_game.TryPlay(x, y, Stone.Black, out _))
            {
                StatusText.Text = "Coup interdit : intersection occupée, suicide ou répétition du ko.";
                return;
            }

            _lastX = x;
            _lastY = y;
            _consecutivePasses = 0;
            UpdateScore();
            DrawBoard();
            await PlayComputerTurnAsync();
        }

        private async void PassButton_Click(object sender, RoutedEventArgs e)
        {
            if (_game is null || _isThinking || _isFinished)
            {
                return;
            }

            RegisterPass(Stone.Black);
            if (!_isFinished)
            {
                await PlayComputerTurnAsync();
            }
        }

        private async Task PlayComputerTurnAsync()
        {
            if (_game is null || _isFinished)
            {
                return;
            }

            GameEngine game = _game;
            _isThinking = true;
            PassButton.IsEnabled = false;
            StatusText.Text = "L’ordinateur réfléchit…";

            try
            {
                await Task.Delay(350);
                (int X, int Y)? move = await Task.Run(() => game.FindComputerMove());
                if (!ReferenceEquals(game, _game) || _isFinished)
                {
                    return;
                }

                if (move is null)
                {
                    RegisterPass(Stone.White);
                    return;
                }

                if (!game.TryPlay(move.Value.X, move.Value.Y, Stone.White, out _))
                {
                    throw new InvalidOperationException("L’ordinateur a proposé un coup illégal.");
                }

                _lastX = move.Value.X;
                _lastY = move.Value.Y;
                _consecutivePasses = 0;
                StatusText.Text = "À vous de jouer — posez une pierre noire.";
                UpdateScore();
                DrawBoard();
            }
            finally
            {
                if (ReferenceEquals(game, _game))
                {
                    _isThinking = false;
                    PassButton.IsEnabled = !_isFinished;
                }
            }
        }

        private void RegisterPass(Stone color)
        {
            _lastX = -1;
            _lastY = -1;
            _consecutivePasses++;
            DrawBoard();

            if (_consecutivePasses == 2)
            {
                _isFinished = true;
                PassButton.IsEnabled = false;
                (double black, double white) = _game!.CalculateScore(Komi);
                string result = black > white
                    ? $"Les Noirs gagnent de {black - white:0.#} point(s)."
                    : white > black
                        ? $"Les Blancs gagnent de {white - black:0.#} point(s)."
                        : "La partie est nulle.";
                StatusText.Text = $"Partie terminée. {result}";
                UpdateScore();
                return;
            }

            if (color == Stone.Black)
            {
                StatusText.Text = "Vous passez. L’ordinateur joue…";
            }
            else
            {
                StatusText.Text = "L’ordinateur passe. À vous de jouer, ou passez pour compter les points.";
            }
        }

        private void UpdateScore()
        {
            if (_game is null)
            {
                return;
            }

            (double black, double white) = _game.CalculateScore(Komi);
            ScoreText.Text = $"Noirs {black:0.#}  ·  Blancs {white:0.#}";
        }

        private void DrawBoard()
        {
            if (_game is null)
            {
                return;
            }

            BoardCanvas.Children.Clear();
            double spacing = (BoardExtent - BoardMargin * 2) / (_boardSize - 1);
            double far = BoardExtent - BoardMargin;

            for (int i = 0; i < _boardSize; i++)
            {
                double position = BoardMargin + i * spacing;
                BoardCanvas.Children.Add(new Line
                {
                    X1 = BoardMargin,
                    Y1 = position,
                    X2 = far,
                    Y2 = position,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                });
                BoardCanvas.Children.Add(new Line
                {
                    X1 = position,
                    Y1 = BoardMargin,
                    X2 = position,
                    Y2 = far,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                });
            }

            foreach (int star in GetStarPoints(_boardSize))
            {
                foreach (int starY in GetStarPoints(_boardSize))
                {
                    double center = BoardMargin + star * spacing;
                    double centerY = BoardMargin + starY * spacing;
                    AddEllipse(center - 3, centerY - 3, 6, Brushes.Black);
                }
            }

            double stoneSize = spacing * 0.84;
            for (int y = 0; y < _boardSize; y++)
            {
                for (int x = 0; x < _boardSize; x++)
                {
                    Stone stone = _game.At(x, y);
                    if (stone == Stone.Empty)
                    {
                        continue;
                    }

                    double centerX = BoardMargin + x * spacing;
                    double centerY = BoardMargin + y * spacing;
                    Brush fill = stone == Stone.Black ? Brushes.Black : Brushes.White;
                    var ellipse = new Ellipse
                    {
                        Width = stoneSize,
                        Height = stoneSize,
                        Fill = fill,
                        Stroke = stone == Stone.Black ? Brushes.Black : Brushes.Gray,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(ellipse, centerX - stoneSize / 2);
                    Canvas.SetTop(ellipse, centerY - stoneSize / 2);
                    BoardCanvas.Children.Add(ellipse);

                    if (x == _lastX && y == _lastY)
                    {
                        AddEllipse(centerX - 4, centerY - 4, 8,
                            stone == Stone.Black ? Brushes.White : Brushes.Black);
                    }
                }
            }
        }

        private void AddEllipse(double left, double top, double size, Brush fill)
        {
            var ellipse = new Ellipse { Width = size, Height = size, Fill = fill };
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            BoardCanvas.Children.Add(ellipse);
        }

        private static int[] GetStarPoints(int size) => size switch
        {
            9 => [2, 4, 6],
            13 => [3, 6, 9],
            19 => [3, 9, 15],
            _ => []
        };
    }

    internal enum Stone : byte
    {
        Empty,
        Black,
        White
    }

    internal sealed class GameEngine
    {
        private readonly int _size;
        private readonly Stone[,] _board;
        private readonly HashSet<string> _positions = [];

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

        private static Stone Opposite(Stone color) =>
            color == Stone.Black ? Stone.White : Stone.Black;
    }
}
