"use client";

import { type ColumnDef, flexRender, getCoreRowModel, useReactTable } from "@tanstack/react-table";
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../components/table";
import { StatusBadge } from "./status-badge";

export interface OrderRow {
  id: string;
  href: string;
  tier: string;
  status: string;
  total: string;
}

const orderColumns: ColumnDef<OrderRow>[] = [
  {
    id: "order",
    header: "Order",
    cell: ({ row }) => (
      <a
        className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
        href={row.original.href}
      >
        {row.original.id}
      </a>
    ),
  },
  {
    accessorKey: "tier",
    header: "Tier",
    cell: ({ row }) => <span className="text-foreground">{row.original.tier}</span>,
  },
  {
    accessorKey: "status",
    header: "Status",
    cell: ({ row }) => <StatusBadge status={row.original.status} />,
  },
  {
    accessorKey: "total",
    header: () => <span className="block text-right">Total</span>,
    cell: ({ row }) => (
      <span className="block text-right tabular-nums text-foreground">{row.original.total}</span>
    ),
  },
];

export interface OrderTableProps {
  rows: OrderRow[];
  caption?: string;
  className?: string;
}

export function OrderTable({ rows, caption, className }: OrderTableProps) {
  const table = useReactTable({
    data: rows,
    columns: orderColumns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <Table aria-label={caption ?? "Orders"} className={className}>
      <TableCaption className="sr-only">{caption ?? "Orders"}</TableCaption>
      <TableHeader>
        {table.getHeaderGroups().map((headerGroup) => (
          <TableRow key={headerGroup.id}>
            {headerGroup.headers.map((header) => (
              <TableHead key={header.id} scope="col">
                {header.isPlaceholder
                  ? null
                  : flexRender(header.column.columnDef.header, header.getContext())}
              </TableHead>
            ))}
          </TableRow>
        ))}
      </TableHeader>
      <TableBody>
        {rows.length === 0 ? (
          <TableRow>
            <TableCell
              className="h-16 text-center text-muted-foreground"
              colSpan={orderColumns.length}
            >
              No orders found.
            </TableCell>
          </TableRow>
        ) : (
          table.getRowModel().rows.map((row) => (
            <TableRow key={row.id}>
              {row.getVisibleCells().map((cell) => (
                <TableCell key={cell.id}>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </TableCell>
              ))}
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  );
}
