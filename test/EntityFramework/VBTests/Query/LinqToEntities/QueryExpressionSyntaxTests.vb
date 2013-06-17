Imports System.Data.Entity.Core.Common.CommandTrees
Imports System.Data.Entity.Core.Common.CommandTrees.Internal
Imports System.Data.Entity.Core.Objects.ELinq
Imports System.Data.Entity.Resources
Imports System.Data.Entity.TestModels.ArubaModel

Public Class QueryExpressionSyntaxTests
    Implements IDisposable

    Private ReadOnly _db As New ArubaContext
    Private ReadOnly _funcletizer As Funcletizer
    Private ReadOnly _printer As New ExpressionPrinter

    Public Sub New()
        _funcletizer = Funcletizer.CreateQueryFuncletizer(_db.InternalContext.ObjectContext)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _db.Dispose()
    End Sub

    <Fact>
    Public Sub Convert_throws_on_from_as_expression_when_no_primitive()
        Dim query = From b As ArubaBaseline In _db.Failures

        Dim ex = Assert.Throws(Of NotSupportedException)(Sub() Convert(query))
        Assert.Equal(Strings.ELinq_UnsupportedCast(GetType(ArubaFailure), GetType(ArubaBaseline)), ex.Message)
    End Sub

    <Fact>
    Public Sub Convert_from_as_expression()
        Dim query = From id As Long In _db.Runs.Select(Function(f) f.Id)

        Dim expression = Convert(query)

        Dim printedExpression = _printer.Print(expression)
        Assert.Equal(
            "Project" & vbCrLf &
            "|_Input : 'LQ2'" & vbCrLf &
            "| |_Project" & vbCrLf &
            "|   |_Input : 'LQ1'" & vbCrLf &
            "|   | |_Scan : ArubaContext.Runs" & vbCrLf &
            "|   |_Projection" & vbCrLf &
            "|     |_Var(LQ1).Id" & vbCrLf &
            "|_Projection" & vbCrLf &
            "  |_Cast(Var(LQ2) As Edm.Int64)",
            printedExpression)
    End Sub

    <Fact>
    Public Sub Convert_group_by_into_expression()
        Dim query = From r In _db.Runs
                    Group By r.Purpose Into Group

        Dim expression = Convert(query)

        Dim printedExpression = _printer.Print(expression)
        Assert.Equal(
            "Project" & vbCrLf &
            "|_Input : 'LQ3'" & vbCrLf &
            "| |_Project" & vbCrLf &
            "|   |_Input : 'LQ2'" & vbCrLf &
            "|   | |_GroupBy" & vbCrLf &
            "|   |   |_Input : 'LQ1', 'GroupLQ1'" & vbCrLf &
            "|   |   | |_Scan : ArubaContext.Runs" & vbCrLf &
            "|   |   |_Keys" & vbCrLf &
            "|   |   | |_Key : 'Key'" & vbCrLf &
            "|   |   |   |_Var(LQ1).Purpose" & vbCrLf &
            "|   |   |_Aggregates" & vbCrLf &
            "|   |     |_Aggregate : 'Group'" & vbCrLf &
            "|   |       |_GroupAggregate" & vbCrLf &
            "|   |         |_Var(GroupLQ1)" & vbCrLf &
            "|   |_Projection" & vbCrLf &
            "|     |_NewInstance : Record['Key'=Edm.Int32, 'Group'=Collection{CodeFirstNamespace.ArubaRun}]" & vbCrLf &
            "|       |_Column : 'Key'" & vbCrLf &
            "|       | |_Var(LQ2).Key" & vbCrLf &
            "|       |_Column : 'Group'" & vbCrLf &
            "|         |_Var(LQ2).Group" & vbCrLf &
            "|_Projection" & vbCrLf &
            "  |_NewInstance : Record['Purpose'=Edm.Int32, 'Group'=Collection{CodeFirstNamespace.ArubaRun}]" & vbCrLf &
            "    |_Column : 'Purpose'" & vbCrLf &
            "    | |_Var(LQ3).Key" & vbCrLf &
            "    |_Column : 'Group'" & vbCrLf &
            "      |_Var(LQ3).Group",
            printedExpression)
    End Sub

    Private Function Convert(query As IQueryable) As DbExpression
        Return New ExpressionConverter(_funcletizer, query.Expression).Convert()
    End Function
End Class
