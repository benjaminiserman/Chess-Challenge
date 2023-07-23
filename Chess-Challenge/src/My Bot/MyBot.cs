﻿using System;
using System.Linq;
using ChessChallenge.API;
using ChessChallenge.Example;

class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        var moves = board.GetLegalMoves().ToArray();
		var random = new Random();

		int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

		var moveToPlay = moves[0];

		var preferredMoves = moves.Where(move =>
			(move.MovePieceType != PieceType.King || move.IsCastles)
			&& !board.SquareIsAttackedByOpponent(move.TargetSquare)
			&& move.PromotionPieceType is PieceType.None or PieceType.Queen
			&& !MoveIsDraw(move)
			&& !AllowsMateInOne(move)
			&& !MoveIsSacrifice(move));

		if (preferredMoves.Any())
		{
			moveToPlay = preferredMoves.First();
		}

		bool isOnStartRows(Square square) => board.IsWhiteToMove
			? square.Rank <= 1
			: square.Rank >= 6;

		var developingMoves = preferredMoves.Where(move => isOnStartRows(move.StartSquare) && !isOnStartRows(move.TargetSquare));
		if (developingMoves.Any())
		{
			moveToPlay = developingMoves.First();
		}

		if (board.PlyCount <= 5)
		{
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
			.Where(move => board.SquareIsAttackedByOpponent(move.StartSquare) && !board.SquareIsAttackedByOpponent(move.TargetSquare));
		if (protectiveMoves.Any())
		{
			moveToPlay = protectiveMoves.First();
		}

		var safeChecks = preferredMoves.Where(MoveIsCheck);
		if (safeChecks.Any())
		{
			moveToPlay = safeChecks.First();
		}

		if (board.PlyCount > 40)
		{
			var pawnMoves = preferredMoves.Where(move => move.MovePieceType is PieceType.Pawn && !board.SquareIsAttackedByOpponent(move.TargetSquare));
			if (pawnMoves.Any())
			{
				moveToPlay = pawnMoves.First();
			}
		}

		var highestValueCapture = 0;
		foreach (var move in moves)
		{
			if (MoveIsCheckmate(move))
			{
				return move;
			}

			if (MoveIsGoodCapture(move, ref highestValueCapture))
			{
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
	}
}