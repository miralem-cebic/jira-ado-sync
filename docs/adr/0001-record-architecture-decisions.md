# 1. Record architecture decisions

Date: 2026-11-12 · Status: accepted

## Context

Decisions without a recorded reason are re-litigated or reversed by accident, by people and by agents. This
project follows [engineering-baseline](https://github.com/miralem-cebic/engineering-baseline), which asks for
recorded decisions (rule GEN-010).

## Decision

Significant decisions are recorded as short ADRs in `docs/adr`, numbered, never edited after acceptance (superseded
by a new ADR instead). Use the `/add-adr` command.

## Consequences

A small cost per decision; a large saving when someone asks "why is it like this".
