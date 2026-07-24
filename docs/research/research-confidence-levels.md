# Research confidence levels

Confidence labels communicate the strength of recorded evidence; they are not mathematical probabilities, legal proof, or substitutes for reproducible experiments.

## Unknown

A question exists, but there is no usable controlled evidence. Unknown behavior must not be invented and should be marked `TODO-D2-RESEARCH` when it affects code or documentation.

## Low

One observation, one experiment, or an ambiguous search match exists. Alternative explanations, including baseline save noise, remain plausible.

## Medium

The observation repeats, but competing explanations remain. Examples include a stable range across two values without a reverse test, or a result not yet separated from save noise.

## High

Several controlled experiments agree, use multiple values, and record evidence. Baseline noise has been considered, but the work may still have explicit limitations.

## ConfirmedByMultipleIndependentExperiments

The result has been repeated independently, checked with several values, checked in reverse or with an alternative value, separated from baseline noise, and found consistent with the other recorded experiments. Evidence and limitations remain attached to the conclusion.

Confidence may decrease when a contradiction, contamination, editor-version difference, or uncontrolled save-time change is discovered. A single byte match can never receive the highest confidence.
