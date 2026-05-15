using System;

class TicTacToe
{
    static char[] board = { '1', '2', '3', '4', '5', '6', '7', '8', '9' };
    static char currentPlayer = 'X';

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Piškvorky (Tic Tac Toe)";

        bool hrajemeDál = true;

        while (hrajemeDál)
        {
            // Reset hry
            board = new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            currentPlayer = 'X';
            bool konecHry = false;

            while (!konecHry)
            {
                Console.Clear();
                VykresliDesku();

                Console.Write($"\nHráč [{currentPlayer}], zadej číslo pole (1-9): ");
                string vstup = Console.ReadLine();

                if (!int.TryParse(vstup, out int tah) || tah < 1 || tah > 9)
                {
                    Console.WriteLine("Neplatný vstup! Stiskni Enter a zkus znovu.");
                    Console.ReadLine();
                    continue;
                }

                if (board[tah - 1] == 'X' || board[tah - 1] == 'O')
                {
                    Console.WriteLine("Toto pole je již obsazeno! Stiskni Enter a zkus znovu.");
                    Console.ReadLine();
                    continue;
                }

                board[tah - 1] = currentPlayer;

                if (ZkontrolujVýhru())
                {
                    Console.Clear();
                    VykresliDesku();
                    Console.WriteLine($"\n🎉 Hráč [{currentPlayer}] vyhrál! Gratulujeme!");
                    konecHry = true;
                }
                else if (ZkontrolujRemízu())
                {
                    Console.Clear();
                    VykresliDesku();
                    Console.WriteLine("\n🤝 Remíza! Nikdo nevyhrál.");
                    konecHry = true;
                }
                else
                {
                    // Přepni hráče
                    currentPlayer = (currentPlayer == 'X') ? 'O' : 'X';
                }
            }

            Console.Write("\nChceš hrát znovu? (a/n): ");
            string odpověď = Console.ReadLine()?.ToLower();
            hrajemeDál = (odpověď == "a" || odpověď == "ano");
        }

        Console.WriteLine("\nDíky za hru! Na shledanou 👋");
    }

    static void VykresliDesku()
    {
        Console.WriteLine("╔═══════════════╗");
        Console.WriteLine("║  TIC TAC TOE  ║");
        Console.WriteLine("╠═══════════════╣");
        Console.WriteLine($"║  {board[0]} │ {board[1]} │ {board[2]}  ║");
        Console.WriteLine("║ ───┼───┼─── ║");
        Console.WriteLine($"║  {board[3]} │ {board[4]} │ {board[5]}  ║");
        Console.WriteLine("║ ───┼───┼─── ║");
        Console.WriteLine($"║  {board[6]} │ {board[7]} │ {board[8]}  ║");
        Console.WriteLine("╚═══════════════╝");
    }

    static bool ZkontrolujVýhru()
    {
        // Řádky
        if (board[0] == board[1] && board[1] == board[2]) return true;
        if (board[3] == board[4] && board[4] == board[5]) return true;
        if (board[6] == board[7] && board[7] == board[8]) return true;

        // Sloupce
        if (board[0] == board[3] && board[3] == board[6]) return true;
        if (board[1] == board[4] && board[4] == board[7]) return true;
        if (board[2] == board[5] && board[5] == board[8]) return true;

        // Diagonály
        if (board[0] == board[4] && board[4] == board[8]) return true;
        if (board[2] == board[4] && board[4] == board[6]) return true;

        return false;
    }

    static bool ZkontrolujRemízu()
    {
        foreach (char pole in board)
        {
            if (pole != 'X' && pole != 'O')
                return false;
        }
        return true;
    }
}