﻿using System;
using System.Linq;
using ChessChallenge.API;
using Microsoft.CodeAnalysis;

class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        var moves = board.GetLegalMoves().ToArray();
		var random = new Random();

		int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

		var moveToPlay = moves[0];

		// en passant is forced (it's the law)
		var enPassant = moves.Where(move => move.IsEnPassant);
		if (enPassant.Any())
		{
			return enPassant.First();
		}

		var preferredMoves = moves
			.Where(move => !board.SquareIsAttackedByOpponent(move.TargetSquare)
				&& !MoveIsDraw(move)
				&& !AllowsMateInOne(move)
				&& !MoveIsSacrifice(move))
			.OrderBy(move => Math.Abs(pieceValues[(int)move.MovePieceType] - 300));

		if (preferredMoves.Any())
		{
			//Console.WriteLine("a");
			//Console.WriteLine(string.Join("\n", preferredMoves.Select(move => $" - {move}")));
			moveToPlay = preferredMoves.First();
		}
		

		bool isOnStartRows(Square square) => board.IsWhiteToMove
			? square.Rank <= 1
			: square.Rank >= 6;

		var developingMoves = preferredMoves.Where(move => isOnStartRows(move.StartSquare) && !isOnStartRows(move.TargetSquare));
		if (developingMoves.Any())
		{
			//Console.WriteLine("b");
			moveToPlay = developingMoves.First();
		}

		if (moves.Any(move => move.IsCastles))
		{
			//Console.WriteLine("castle");
			moveToPlay = moves.First(move => move.IsCastles);
		}

		if (board.PlyCount <= 5) // always play for scholar's mate
		{
			//Console.WriteLine("c");
			var gotMove = preferredMoves.Where(move => board.PlyCount switch
			{
				0 => move.MovePieceType == PieceType.Pawn && move.TargetSquare == new Square("e4"),
				1 => move.MovePieceType == PieceType.Pawn && move.TargetSquare == new Square("e5"),
				2 => move.MovePieceType == PieceType.Bishop && move.TargetSquare == new Square("c4"),
				3 => move.MovePieceType == PieceType.Bishop && move.TargetSquare == new Square("c5"),
				4 => move.MovePieceType == PieceType.Queen && move.TargetSquare == new Square("f3"),
				5 => move.MovePieceType == PieceType.Queen && move.TargetSquare == new Square("f6")
			});

			if (gotMove.Any())
			{
				moveToPlay = gotMove.First();
			}
		}

		var protectiveMoves = moves
			.Where(move => board.SquareIsAttackedByOpponent(move.StartSquare)
				&& !IsProtected(move.StartSquare, board)
				&& (!board.SquareIsAttackedByOpponent(move.TargetSquare)
					|| pieceValues[(int)move.CapturePieceType] >= pieceValues[(int)move.MovePieceType]));
		if (protectiveMoves.Any())
		{
			//Console.WriteLine("d");
			moveToPlay = protectiveMoves.Where(move => preferredMoves.Contains(move)).FirstOrDefault();
			if (moveToPlay == default)
			{
				moveToPlay = protectiveMoves.First();
			}
		}

		var safeChecks = preferredMoves.Where(MoveIsCheck);
		if (safeChecks.Any())
		{
			//Console.WriteLine("e");
			moveToPlay = safeChecks.First();
		}

		if (board.PlyCount > 40)
		{
			var pawnMoves = preferredMoves.Where(move => move.MovePieceType is PieceType.Pawn 
				&& !board.SquareIsAttackedByOpponent(move.TargetSquare));
			if (pawnMoves.Any())
			{
				//Console.WriteLine("f");
				moveToPlay = pawnMoves.First();
			}
		}

		var promotionMoves = preferredMoves.Where(move => move.PromotionPieceType == PieceType.Queen);
		if (promotionMoves.Any())
		{
			//Console.WriteLine("g");
			moveToPlay = promotionMoves.First();
		}

		var highestValueCapture = 0;
		foreach (var move in moves)
		{
			if (MoveIsCheckmate(move))
			{
				return move;
			}

			if (MoveIsGoodCapture(move, ref highestValueCapture) && !MoveIsSacrifice(move))
			{
				//Console.WriteLine("h");
				moveToPlay = move;
			}
		}

		return moveToPlay;

		bool MoveIsGoodCapture(Move move, ref int highestValueCapture)
		{
			var capturedPieceValue = pieceValues[(int)move.CapturePieceType];
			var myPieceValue = pieceValues[(int)move.MovePieceType];

			if (board.SquareIsAttackedByOpponent(move.TargetSquare))
			{
				capturedPieceValue -= myPieceValue;
			}
			else if (MoveIsCheck(move))
			{
				capturedPieceValue += 1;
			}

			if (capturedPieceValue > highestValueCapture)
			{
				highestValueCapture = capturedPieceValue;
				return true;
			}

			return false;
		}

		bool MoveIsCheckmate(Move move)
		{
			board.MakeMove(move);
			var isMate = board.IsInCheckmate();
			board.UndoMove(move);
			return isMate;
		}

		bool MoveIsCheck(Move move)
		{
			board.MakeMove(move);
			var isMate = board.IsInCheck();
			board.UndoMove(move);
			return isMate;
		}

		bool MoveIsDraw(Move move)
		{
			board.MakeMove(move);
			var isDraw = board.IsDraw();
			board.UndoMove(move);
			return isDraw;
		}

		bool MoveIsSacrifice(Move move)
		{
			var takenValue = pieceValues[(int)move.CapturePieceType];
			board.MakeMove(move);
			foreach (var opponentMove in board.GetLegalMoves())
			{
				var bestCaptureValue = 0;
				if (MoveIsGoodCapture(opponentMove, ref bestCaptureValue) && bestCaptureValue > takenValue)
				{
					board.UndoMove(move);
					return true;
				}
			}

			board.UndoMove(move);
			return false;
		}

		bool AllowsMateInOne(Move move)
		{
			board.MakeMove(move);
			foreach (var opponentMove in board.GetLegalMoves())
			{
				if (MoveIsCheckmate(opponentMove))
				{
					board.UndoMove(move);
					return true;
				}
			}

			board.UndoMove(move);
			return false;
		}

		bool IsProtected(Square square, Board board)
		{
			if (board.TrySkipTurn())
			{
				var isAttacked = board.SquareIsAttackedByOpponent(square);
				board.UndoSkipTurn();
				return isAttacked;
			}

			return true;
		}
	}
}