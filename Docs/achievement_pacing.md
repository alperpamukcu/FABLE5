# Achievement pacing — the floor bot

Written by `LastCall → Achievement Pacing`. **200** seeded bars, horizon 90 nights, each
with a fresh ledger (a new player's first bar). The bot is a FLOOR: it plays at one standard,
never learns, and goes bankrupt young — so a late achievement it does not reach is expected, an
early one it does not reach is a design bug. Nights are counted from 1; a bar's night is the
one whose books just closed (the market comes before, so purchases count on their own night).

Nights played p25/median/p75: 18 / 22 / 22.
Longest run of nights with nothing earned, p25/median/p75: 2 / 4 / 5.

## How many land on each night

| night | bars still open | unlocks per open bar | earned by then (per bar) |
|--:|--:|--:|--:|
| 1 | 200 | 3.03 | 3.03 |
| 2 | 200 | 1.71 | 4.74 |
| 3 | 200 | 1.04 | 5.78 |
| 4 | 200 | 2.43 | 8.20 |
| 5 | 200 | 1.85 | 10.05 |
| 6 | 200 | 1.77 | 11.82 |
| 7 | 200 | 1.66 | 13.48 |
| 8 | 200 | 0.96 | 14.44 |
| 9 | 200 | 1.05 | 15.49 |
| 10 | 200 | 0.76 | 16.25 |
| 11 | 200 | 1.13 | 17.37 |
| 12 | 200 | 1.28 | 18.65 |
| 13 | 200 | 0.23 | 18.88 |
| 14 | 200 | 0.20 | 19.08 |
| 15 | 200 | 0.67 | 19.74 |
| 16 | 200 | 0.65 | 20.39 |
| 17 | 191 | 0.30 | 20.68 |
| 18 | 150 | 0.19 | 20.83 |
| 19 | 134 | 0.07 | 20.88 |
| 20 | 128 | 0.10 | 20.94 |
| 21 | 122 | 0.23 | 21.08 |
| 22 | 101 | 0.78 | 21.48 |
| 23 | 29 | 0.83 | 21.60 |
| 24 | 6 | 2.00 | 21.66 |

## Each achievement

| id | tier | name | bars that earned it | night: p25 / median / p75 |
|---|---|---|--:|--:|
| FIRST_ROUND | opening | First Round | 200 of 200 (100.00%) | 1 / 1 / 1 |
| LIGHTS_OUT | opening | Lights Out | 200 of 200 (100.00%) | 1 / 1 / 1 |
| A_NEW_PAGE | opening | A New Page | 200 of 200 (100.00%) | 2 / 2 / 2 |
| HARD_SHAKE | week | Hard Shake | 200 of 200 (100.00%) | 4 / 4 / 4 |
| GOOD_HEADS | week | Good Heads | 200 of 200 (100.00%) | 4 / 5 / 5 |
| GETTING_THE_HANG | week | Getting the Hang of It | 200 of 200 (100.00%) | 3 / 4 / 4 |
| ELBOW_GREASE | week | Elbow Grease | 200 of 200 (100.00%) | 6 / 7 / 7 |
| SAME_AGAIN | week | Same Again | 200 of 200 (100.00%) | 6 / 7 / 9 |
| STOCKING_UP | week | Stocking Up | 200 of 200 (100.00%) | 5 / 5 / 5 |
| HOME_IMPROVEMENT | week | Home Improvement | 200 of 200 (100.00%) | 1 / 1 / 1 |
| TALK_OF_THE_STREET | week | Talk of the Street | 200 of 200 (100.00%) | 9 / 10 / 10 |
| SIX_NIGHTS | week | Six Nights a Week | 200 of 200 (100.00%) | 6 / 6 / 6 |
| THE_USUAL | week | The Usual | 200 of 200 (100.00%) | 2 / 2 / 3 |
| A_WEEKS_WORK | week | A Week's Work | 196 of 200 (98.00%) | 3 / 3 / 4 |
| THE_BLOCKS_BEST | early | The Block's Best | 76 of 200 (38.00%) | 12 / 13 / 14 |
| NOT_TONIGHT | early | Not Tonight | 66 of 200 (33.00%) | 14 / 15 / 16 |
| HUNDRED_CLUB | early | Hundred Club | 200 of 200 (100.00%) | 11 / 11 / 11 |
| TIP_JAR | early | Tip Jar | 200 of 200 (100.00%) | 15 / 15 / 16 |
| CLEAN_SHEET | early | Clean Sheet | 196 of 200 (98.00%) | 8 / 9 / 9 |
| TWO_MORE_STOOLS | early | Two More Stools | 200 of 200 (100.00%) | 7 / 7 / 8 |
| A_FORTNIGHT_OPEN | early | A Fortnight Open | 200 of 200 (100.00%) | 12 / 12 / 12 |
| LAST_ORDERS | early | Last Orders | 200 of 200 (100.00%) | 18 / 22 / 22 |
| CONFIDANT | early | Confidant | 174 of 200 (87.00%) | 8 / 10 / 14 |
| TALK_OF_THE_TOWN | mid | Talk of the Town | 0 of 200 (0.00%) | — |
| STIRRED_NOT_SHAKEN | mid | Stirred, Not Shaken | 0 of 200 (0.00%) | — |
| WELL_READ | mid | Well Read | 0 of 200 (0.00%) | — |
| DOWN_TO_THE_DROP | mid | Down to the Drop | 16 of 200 (8.00%) | 17 / 20 / 22 |
| THE_CITYS_BEST | mid | The City's Best | 0 of 200 (0.00%) | — |
| SEEN_THEM_ALL | mid | Seen Them All | 1 of 200 (0.50%) | 16 / 16 / 16 |
| BIG_NIGHT | mid | Big Night | 0 of 200 (0.00%) | — |
| TAPMASTER | mid | Tapmaster | 0 of 200 (0.00%) | — |
| THE_BOUNCER | mid | The Bouncer | 0 of 200 (0.00%) | — |
| FOUR_WEEKS_OPEN | mid | Four Weeks Open | 6 of 200 (3.00%) | 24 / 24 / 24 |
| NIGHT_OWL | mid | Night Owl | 0 of 200 (0.00%) | — |
| FIVE_HUNDRED_POURS | mid | Five Hundred Pours | 0 of 200 (0.00%) | — |
| DISH_PIG | mid | Dish Pig | 0 of 200 (0.00%) | — |
| TEN_OUT_OF_TEN | late | Ten Out of Ten | 0 of 200 (0.00%) | — |
| A_THOUSAND_POURS | late | A Thousand Pours | 0 of 200 (0.00%) | — |
| THE_COUNTRYS_BEST | late | The Country's Best | 0 of 200 (0.00%) | — |
| FIVE_STARS_TONIGHT | late | Five Stars Tonight | 0 of 200 (0.00%) | — |
| MONEY_IN_THE_BANK | late | Money in the Bank | 0 of 200 (0.00%) | — |
| LEGENDARY_GLASSWARE | late | Legendary Glassware | 0 of 200 (0.00%) | — |
| THE_BEST_THERE_IS | late | The Best There Is | 0 of 200 (0.00%) | — |
| FIVE_STAR_ROOM | late | Five-Star Room | 0 of 200 (0.00%) | — |
| THE_WHOLE_BOOK | late | The Whole Book | 0 of 200 (0.00%) | — |
| AN_INSTITUTION | late | An Institution | 0 of 200 (0.00%) | — |
| WRONG_CALL | secret | Wrong Call | 0 of 200 (0.00%) | — |
| BLOWOUT | secret | Blowout | 0 of 200 (0.00%) | — |
