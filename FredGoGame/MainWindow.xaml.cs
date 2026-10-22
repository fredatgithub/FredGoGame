using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using Path = System.IO.Path;
using Stone = FredGoGame.Enumerations.Stone;

namespace FredGoGame
{
  public partial class MainWindow: Window
  {
    private const double BoardExtent = 600;
    private const double BoardMargin = 32;
    private const double Komi = 6.5;
    private readonly List<(Stone Color, int? X, int? Y)> _moves = [];
    private GameEngine? _game;
    private int _boardSize = 9;
    private int _consecutivePasses;
    private int _lastX = -1;
    private int _lastY = -1;
    private bool _isThinking;
    private bool _isFinished;
    private bool _isChangingBoardSize;
    private bool _currentGameSaved;

    public MainWindow()
    {
      InitializeComponent();
      string version = Assembly.GetExecutingAssembly()
          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
          ?? throw new InvalidOperationException("La version de l’application est absente.");
      Title = $"Fred Go — Jouer au Go - Version {version}";
      StartNewGame();
    }

    private bool StartNewGame()
    {
      if (_game is not null && !SaveCurrentGame())
      {
        return false;
      }

      _boardSize = BoardSizeSelector.SelectedIndex switch
      {
        1 => 13,
        2 => 19,
        _ => 9
      };
      _game = new GameEngine(_boardSize);
      _moves.Clear();
      _currentGameSaved = false;
      _consecutivePasses = 0;
      _lastX = -1;
      _lastY = -1;
      _isThinking = false;
      _isFinished = false;
      StatusText.Text = "À vous de jouer — posez une pierre noire.";
      PassButton.IsEnabled = true;
      UpdateScore();
      DrawBoard();
      return true;
    }

    private void BoardSizeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (_game is not null && !_isChangingBoardSize && !StartNewGame())
      {
        _isChangingBoardSize = true;
        BoardSizeSelector.SelectedIndex = _boardSize switch
        {
          13 => 1,
          19 => 2,
          _ => 0
        };
        _isChangingBoardSize = false;
      }
    }

    private void NewGameButton_Click(object sender, RoutedEventArgs e) => StartNewGame();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
      e.Cancel = !SaveCurrentGame();
    }

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

      _moves.Add((Stone.Black, x, y));
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

        _moves.Add((Stone.White, move.Value.X, move.Value.Y));
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
      _moves.Add((color, null, null));
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
        SaveCurrentGame();
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

    private bool SaveCurrentGame()
    {
      if (_game is null || _currentGameSaved)
      {
        return true;
      }

      try
      {
        var sgf = new StringBuilder();
        sgf.Append("(;GM[1]FF[4]CA[UTF-8]AP[FredGoGame]SZ[")
            .Append(_boardSize)
            .Append("]KM[")
            .Append(Komi.ToString("0.#", CultureInfo.InvariantCulture))
            .Append("]PB[joueurHumain]PW[joueurOrdinateur]DT[")
            .Append(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append(']');

        if (_isFinished)
        {
          (double black, double white) = _game.CalculateScore(Komi);
          string result = black > white
              ? $"B+{(black - white).ToString("0.#", CultureInfo.InvariantCulture)}"
              : white > black
                  ? $"W+{(white - black).ToString("0.#", CultureInfo.InvariantCulture)}"
                  : "0";
          sgf.Append("RE[").Append(result).Append(']');
        }

        foreach ((Stone color, int? x, int? y) in _moves)
        {
          sgf.Append(';').Append(color == Stone.Black ? 'B' : 'W').Append('[');
          if (x.HasValue && y.HasValue)
          {
            sgf.Append((char)('a' + x.Value)).Append((char)('a' + y.Value));
          }
          sgf.Append(']');
        }
        sgf.Append(')');

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Go-Games");
        Directory.CreateDirectory(directory);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        string baseName = $"Go-Game-{timestamp}";
        for (int suffix = 1; ; suffix++)
        {
          string fileName = suffix == 1 ? $"{baseName}.sgf" : $"{baseName}-{suffix}.sgf";
          string path = Path.Combine(directory, fileName);
          FileStream stream;
          try
          {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
          }
          catch (IOException) when (File.Exists(path))
          {
            continue;
          }

          using (stream)
          using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
          {
            writer.Write(sgf);
          }
          _currentGameSaved = true;
          return true;
        }
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                 ArgumentException or NotSupportedException or
                                 System.Security.SecurityException)
      {
        MessageBox.Show(
            $"La partie n’a pas pu être enregistrée dans Documents\\Go-Games.\n\n{ex.Message}",
            "Enregistrement impossible",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return false;
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
}
