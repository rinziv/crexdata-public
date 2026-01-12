import pandas as pd
import numpy as np


class BufferProc:

    def __init__(
        self,
        saccade_threshold,
        blink_threshold,
        angVelocity_threshold,
        angAcceleration_threshold,
    ):

        self.blink_threshold = blink_threshold
        self.saccade_threshold = saccade_threshold
        self.angVelocity_threshold = angVelocity_threshold
        self.angAcceleration_threshold = angAcceleration_threshold

    def aloneBlink(self, buff):
        is_blink = buff["label"] == "blink"

        # Check previous and next
        prev_is_blink = is_blink.shift(1, fill_value=False)
        next_is_blink = is_blink.shift(-1, fill_value=False)

        # Alone if current is blink and neither previous nor next is blink
        alone = is_blink & (~prev_is_blink) & (~next_is_blink)

        # Add result as a column for inspection
        buff["is_blink"] = is_blink
        buff["is_alone"] = alone
        buff["label"] = buff.apply(
            lambda row: (
                self.label_condition(row) if row["is_alone"] == True else row["label"]
            ),
            axis=1,
        )
        return buff.drop(columns=["is_blink", "is_alone"], errors="ignore")

    def label_condition(self, row):
        if (
            row["angVelocity"] > self.angVelocity_threshold
            and abs(row["angAcceleration"]) > self.angAcceleration_threshold
        ):
            return "saccade"
        elif (
            row["angVelocity"] < self.angVelocity_threshold
            and abs(row["angAcceleration"]) < self.angAcceleration_threshold
        ):
            return "fixation"
        else:
            return "normal"

    def buffer_proc(self, buff, firstElementIsBlinkOrFlicker, helping_row):
        if firstElementIsBlinkOrFlicker:
            buff = pd.concat([helping_row, buff], ignore_index=True)
        contains_blink_or_flicker = buff["label"].str.contains("blink|flicker").any()
        try:
            if contains_blink_or_flicker:
                # print("Contains blink or flicker")
                # print("buff TYPES BOFERO NAN:", buff.dtypes)
                buff.loc[
                    buff["label"].str.contains("blink|flicker"),
                    [
                        "pupilSize",
                        "pupCntr_x",
                        "pupCntr_y",
                        "angDistance",
                        "angVelocity",
                        "angAcceleration",
                    ],
                ] = np.nan
                buff[
                    [
                        "pupilSize",
                        "pupCntr_x",
                        "pupCntr_y",
                        "angDistance",
                        "angVelocity",
                        "angAcceleration",
                    ]
                ] = buff[
                    [
                        "pupilSize",
                        "pupCntr_x",
                        "pupCntr_y",
                        "angDistance",
                        "angVelocity",
                        "angAcceleration",
                    ]
                ].interpolate(
                    method="linear"
                )
        except Exception as e:
            print(f"An error occurred while processing frame: {e}")
            print(buff.dtypes)
            print(helping_row.dtypes)
        buff["label"] = buff.apply(
            lambda row: (
                self.label_condition(row) if row["label"] == "flicker" else row["label"]
            ),
            axis=1,
        )

        buff["group"] = (buff["label"] != buff["label"].shift()).cumsum()
        group_counts = buff.groupby("group").transform("size")
        buff["label"] = buff.apply(
            lambda x: (
                "normal"
                if x["label"] == "fixation"
                and group_counts[x.name] < self.saccade_threshold
                else (
                    "normal"
                    if x["label"] == "saccade"
                    and group_counts[x.name] > self.saccade_threshold
                    else (
                        "closed"
                        if x["label"] == "blink"
                        and group_counts[x.name] > self.blink_threshold
                        else x["label"]
                    )
                )
            ),
            axis=1,
        )
        buff = buff.drop(columns=["group"], errors="ignore")
        if firstElementIsBlinkOrFlicker:
            buff = buff.iloc[1:].reset_index(drop=True)
        buff = self.aloneBlink(buff)
        return buff
